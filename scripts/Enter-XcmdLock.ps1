# @implements ReleaseHandoff, PackageInputs
param([Parameter(Mandatory)][string]$Path)

$key = [System.IO.Path]::GetFullPath($Path).ToUpperInvariant()
$hash = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($key)))
$mutex = [System.Threading.Mutex]::new($false, ('Local\Xcmd-' + $hash))
try {
    try {
        if (-not $mutex.WaitOne([TimeSpan]::FromMinutes(3))) { throw 'Timed out waiting for another Xcmd build or release.' }
    }
    catch [System.Threading.AbandonedMutexException] {}
    $mutex
}
catch {
    $mutex.Dispose()
    throw
}
