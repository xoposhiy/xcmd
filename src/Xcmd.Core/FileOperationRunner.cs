namespace Xcmd.Core;

public sealed class FileOperationRunner(IFileOperationStore store, Func<FileConflict, CancellationToken, Task<ConflictDecision>> resolveConflict,
    Func<FileOperationError, CancellationToken, Task<FileErrorAction>>? resolveError = null)
{
    public async Task RunAsync(FileOperationJob job, Action changed)
    {
        var run = new Run(store, resolveConflict, resolveError, job, changed);
        await run.ExecuteAsync();
    }

    private sealed class Run(IFileOperationStore store, Func<FileConflict, CancellationToken, Task<ConflictDecision>> resolveConflict,
        Func<FileOperationError, CancellationToken, Task<FileErrorAction>>? resolveError, FileOperationJob job, Action changed)
    {
        private readonly Dictionary<(bool, bool), ConflictDecision> decisions = [];
        private readonly HashSet<Type> skippedErrors = [];
        private readonly HashSet<(string Source, string Destination)> copiedFiles = [];
        private CancellationToken Token => job.Token;

        public async Task ExecuteAsync()
        {
            job.State = FileOperationState.Counting;
            changed();
            var index = 0;
            try
            {
                FileOperationRules.ValidateRequest(job.Request);
                await FileOperationInventory.CountAsync(job, store, changed);
                Token.ThrowIfCancellationRequested();
                job.State = FileOperationState.Running; changed();
                if (job.Request.Kind == FileOperationKind.CreateZip) await CreateZipAsync();
                else for (; index < job.Request.Sources.Length; index++)
                    {
                        Token.ThrowIfCancellationRequested();
                        var source = job.Request.Sources[index];
                        var destination = job.Request.Destination == null ? null : Path.Combine(job.Request.Destination, job.Request.NewName ?? source.Name);
                        await AttemptAsync(source, destination);
                    }
                Token.ThrowIfCancellationRequested();
                job.State = job.FailedEntries > 0 ? FileOperationState.CompletedWithErrors
                    : job.Outcomes.Any(result => result.Kind == FileOutcomeKind.Partial) ? FileOperationState.CompletedWithSkippedItems : FileOperationState.Completed;
            }
            catch (OperationCanceledException) when (Token.IsCancellationRequested)
            {
                job.State = FileOperationState.Cancelled;
                job.Outcomes.Add(new(job.CurrentPath ?? "", null, FileOutcomeKind.Cancelled, "Completed changes remain. The current item may be partial."));
                job.Outcomes.AddRange(job.Request.Sources.Skip(index + 1).Select(entry => new FileOutcome(entry.FullPath, null, FileOutcomeKind.NotStarted)));
            }
            changed();
        }

        private async Task CreateZipAsync()
        {
            var destination = Path.Combine(job.Request.Destination!, job.Request.NewName!);
            await CheckParentsAsync(job.Request.Destination!);
            while (true)
            {
                var writing = true;
                var progress = new Progress<long>(bytes =>
                {
                    if (!writing || job.State != FileOperationState.Running) return;
                    job.BytesTransferred = bytes; job.CurrentFileBytes = bytes; changed();
                });
                try
                {
                    job.CurrentPath = destination;
                    job.BytesTransferred = await store.CreateZipAsync(job.Request.Sources, destination, progress, Token);
                    foreach (var source in job.Request.Sources) Complete(source, destination, wholeTree: true);
                    return;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    writing = false; job.CurrentFileBytes = 0; job.BytesTransferred = 0;
                    Token.ThrowIfCancellationRequested();
                    if (await DecideErrorAsync(job.Request.Sources[0], destination, error) == FileErrorAction.Retry) continue;
                    foreach (var source in job.Request.Sources) job.Record(source, destination, FileOutcomeKind.Failed, error.Message, wholeTree: true);
                    return;
                }
                finally { writing = false; }
            }
        }

        private async Task<bool> AttemptAsync(FileEntry source, string? destination)
        {
            while (true)
            {
                try { return await AttemptOnceAsync(source, destination); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    Token.ThrowIfCancellationRequested();
                    var action = await DecideErrorAsync(source, destination, error);
                    if (action == FileErrorAction.Retry) continue;
                    job.Record(source, destination, FileOutcomeKind.Failed, error.Message);
                    changed();
                    return false;
                }
            }
        }

        private async Task<FileErrorAction> DecideErrorAsync(FileEntry source, string? destination, Exception error)
        {
            if (resolveError == null || skippedErrors.Contains(error.GetType())) return FileErrorAction.Skip;
            job.State = FileOperationState.WaitingForError; changed();
            var action = await resolveError(FileOperationError.FromException(source, destination, error), Token);
            if (action == FileErrorAction.Cancel) job.Cancel();
            Token.ThrowIfCancellationRequested();
            if (action == FileErrorAction.SkipAll) skippedErrors.Add(error.GetType());
            job.State = FileOperationState.Running; changed();
            return action;
        }

        private async Task<bool> AttemptOnceAsync(FileEntry source, string? destination)
        {
            Token.ThrowIfCancellationRequested();
            job.CurrentPath = source.FullPath;
            job.CurrentFileBytes = 0;
            changed();
            var current = await store.InspectAsync(source.FullPath, Token) ?? throw new IOException("The source no longer exists.");
            await CheckParentsAsync(Path.GetDirectoryName(source.FullPath)!);
            if (job.Request.Kind is FileOperationKind.Recycle or FileOperationKind.Delete)
                return await RemoveAsync(current);
            if (destination == null) throw new ArgumentException("Choose a destination directory.");
            FileOperationRules.ValidateTarget(current, destination);
            if (current.IsLink) throw new IOException("Copying and moving links is not supported. The link was left unchanged.");
            await CheckParentsAsync(Path.GetDirectoryName(destination)!);
            return await TransferAsync(current, destination);
        }

        private Task CheckParentsAsync(string path) => store.ValidateDirectoryAsync(path, Token);

        private async Task<bool> TransferAsync(FileEntry source, string destination)
        {
            var target = await store.InspectAsync(destination, Token);
            if (target != null && await MatchesAsync(source, target))
            {
                if (job.Request.Kind == FileOperationKind.Move && copiedFiles.Contains((source.FullPath, destination)))
                { await store.RemoveAsync(source, false, Token); Complete(source, destination); return true; }
                job.Record(source, destination, FileOutcomeKind.Skipped, "Contents and metadata match."); changed();
                return job.Request.Kind == FileOperationKind.Copy;
            }
            var replace = false;
            if (target != null && !(source.IsDirectory && target.IsDirectory && !target.IsLink))
            {
                var decision = await DecideAsync(source, target);
                if (decision.Action == ConflictAction.Skip)
                { job.Record(source, destination, FileOutcomeKind.Skipped, wholeTree: source.IsDirectory); changed(); return job.Request.Kind == FileOperationKind.Copy; }
                if (decision.Action == ConflictAction.Rename)
                {
                    var error = PanelRules.ValidateName(decision.NewName ?? "");
                    if (error != null) throw new ArgumentException(error);
                    return await AttemptAsync(source, Path.Combine(Path.GetDirectoryName(destination)!, decision.NewName!));
                }
                if (source.IsDirectory || target.IsDirectory || target.IsLink) throw new IOException("Replace is available only for regular files. Skip this item or choose another name.");
                replace = true;
            }
            if (job.Request.Kind == FileOperationKind.Move && (target == null || replace) && await store.TryMoveAsync(source, destination, replace, Token))
            { Complete(source, destination, wholeTree: source.IsDirectory); return true; }
            if (!source.IsDirectory)
                return await CopyFileAsync(source, destination, replace);
            return await CopyFolderAsync(source, destination, target == null);
        }

        private async Task<bool> MatchesAsync(FileEntry source, FileEntry target) =>
            !source.IsDirectory && !target.IsDirectory && !target.IsLink && source.Size == target.Size &&
            source.ModifiedUtc == target.ModifiedUtc && await store.AreIdenticalAsync(source, target, Token);

        private async Task<bool> CopyFolderAsync(FileEntry source, string destination, bool create)
        {
            if (create) await store.CreateFolderAsync(destination, Token);
            var complete = true;
            foreach (var child in await store.ReadDirectoryAsync(source.FullPath, Token))
                complete = await AttemptAsync(child, Path.Combine(destination, child.Name)) && complete;
            if (create && complete) await store.CopyMetadataAsync(source.FullPath, destination, Token);
            if (complete && job.Request.Kind == FileOperationKind.Move) await store.RemoveAsync(source, false, Token);
            job.Record(source, destination, complete ? FileOutcomeKind.Completed : FileOutcomeKind.Partial,
                complete ? null : "Some items were skipped or failed. Remaining source items were kept.");
            changed();
            return complete;
        }

        private async Task<bool> CopyFileAsync(FileEntry source, string destination, bool replace)
        {
            var before = job.BytesTransferred;
            var transferring = true;
            var progress = new Progress<long>(bytes =>
            {
                if (!transferring || job.State != FileOperationState.Running || job.CurrentPath != source.FullPath) return;
                job.BytesTransferred = before + bytes; job.CurrentFileBytes = bytes; changed();
            });
            long copied;
            try { copied = await store.CopyAsync(source.FullPath, destination, replace, progress, Token); }
            catch { job.BytesTransferred = before; job.CurrentFileBytes = 0; throw; }
            finally { transferring = false; }
            job.BytesTransferred = before + copied;
            copiedFiles.Add((source.FullPath, destination));
            if (job.Request.Kind == FileOperationKind.Move) await store.RemoveAsync(source, false, Token);
            Complete(source, destination);
            return true;
        }

        private async Task<ConflictDecision> DecideAsync(FileEntry source, FileEntry target)
        {
            var key = (source.IsDirectory, target.IsDirectory || target.IsLink);
            if (decisions.TryGetValue(key, out var saved)) return saved;
            job.State = FileOperationState.WaitingForConflict; changed();
            var decision = await resolveConflict(new(source, target), Token);
            if (decision.Action == ConflictAction.Cancel) job.Cancel();
            Token.ThrowIfCancellationRequested();
            job.State = FileOperationState.Running; changed();
            if (decision.ApplyToAll && decision.Action is ConflictAction.Skip or ConflictAction.Replace) decisions[key] = decision;
            return decision;
        }

        private async Task<bool> RemoveAsync(FileEntry entry)
        {
            var complete = true;
            if (job.Request.Kind != FileOperationKind.Recycle && entry.IsDirectory && !entry.IsLink)
                foreach (var child in await store.ReadDirectoryAsync(entry.FullPath, Token))
                    complete = await AttemptAsync(child, null) && complete;
            if (complete) await store.RemoveAsync(entry, job.Request.Kind == FileOperationKind.Recycle, Token);
            job.Record(entry, null, complete ? FileOutcomeKind.Completed : FileOutcomeKind.Partial,
                complete ? null : "Some items could not be removed. The folder was kept.", wholeTree: job.Request.Kind == FileOperationKind.Recycle && complete);
            changed();
            return complete;
        }

        private void Complete(FileEntry source, string destination, bool wholeTree = false)
        { job.Record(source, destination, FileOutcomeKind.Completed, wholeTree: wholeTree); changed(); }
    }
}
