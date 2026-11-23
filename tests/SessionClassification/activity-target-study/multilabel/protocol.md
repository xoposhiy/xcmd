# Agreement study

## Samples and blinding

Use all 32 completed command and patch records in the previously double-reviewed session. Use eight named development calls from Point Taken as a separate challenge sample. These calls illustrate literal reads, compound queries, patches and scripts. They are intentionally selected and cannot estimate how common such calls are. Use Point Taken lines 31, 39, 64, 77, 96, 110, 146 and 198. Keep each completed command or patch as one observation. Sibling calls inside a parallel wrapper keep their own IDs.

Two new independent Codex reviewers get the same trace, recorded results, the local repository and permission to inspect historical files. Each first seals one pair per call using the old guide. Both single-pair files must be sealed before the reviewers get the multi-pair instructions. No reviewer receives old labels, classifier output or the other reviewer's labels. Use no external API. Record which context sources were actually consulted. Full trace access and file availability do not guarantee that every reviewer used each piece of context.

## Single-pair guide

Assign one target and one action to the actual operation. Use source for the shipped implementation and assets, tests for test code and fixtures, docs for project documentation and instructions including README anywhere, tooling for developer tooling, build files, installed skills and packages, git for Git history, queries, refs or index, tracker for issue records, mixed for multiple substantive roles or known project-wide scope, unknown for an unestablished target. Read includes content, listings, search, metadata, Git diffs and issue queries, including failed read attempts. Write requires a confirmed mutation. Other includes execution, a build or a test and a failed mutation with no confirmed effect. Unknown means that the action is unestablished. Confirmed writes have priority in a compound call. Ignore incidental internal I/O and paths merely mentioned in output.

## Multi-pair guide

Keep target and action attached to each other. Record every separately evidenced, substantive resource and operation. Deduplicate identical pairs within each call. Use the single-pair vocabulary with one change in display: project denotes an observed repository-wide query. Multiple explicit resources receive their own roles. Preserve both read and write when both happened. A read attempt can be evidenced even when content delivery failed. Do not invent a content read from a listing, a name in stdout or an implicit implementation detail of a patch. Listing a directory is a read of its scope.

Simultaneous operations and uncertainty are different observations. If a single resource has two plausible roles and neither can be established, use unknown for that axis and list the alternatives separately. Unknown may accompany an established pair only for a separate unresolved operation. Include a short evidence cue for each pair. No primary tag, probabilities or weights are required. Preserve the unit of account and the same trace access across both passes.

## Measurements fixed before labels

Report metrics per sample. For the single-pair pass, measure exact pair, target and action agreement. For the multi-pair pass report exact set agreement, mean per-call Jaccard similarity, and symmetric micro-F1 from total pair overlap. Also report the cardinality of the reviewers' sets, counts of calls each marked as multi-pair, and mean Jaccard on calls either reviewer marked multi-pair. Show separate target and action set agreement. Match unknown literally; show the number of calls with unknowns and a Jaccard sensitivity check dropping unknown-axis pairs and excluding empty unions. Report no score as accuracy without an adjudicated reference.

Compare both new passes on the same calls. Report exact-match gains and losses, split versus a collapse of the multi-pair answers back into single pairs, and overlap on the calls disputed in the historical review. These are descriptive results from a fixed order and the same model family. An independent auditor can inspect the original conflicting labels and rationales only to publish aggregate counts by plausible cause. No examples or per-call labels from the blind session can be used for classifier development.
