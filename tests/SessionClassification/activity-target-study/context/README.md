# Project context comparison

The experiment compares the earlier file-memory classifier with a candidate that adds PowerShell syntax, historical project facts and result-shape evidence. The primary outcome is the number of calls for which both labels match. The additional blind session was selected and labelled by an independent reviewer. The earlier two validation sessions remain sealed and are scored only in aggregate.

## Reproduce the results offline

From the repository root run:

```
python -B tests/SessionClassification/activity-target-study/context/trial.py score
python -B tests/SessionClassification/activity-target-study/context/literal_trial.py score
python -B tests/SessionClassification/activity-target-study/context/agreement.py
python -B tests/SessionClassification/activity-target-study/context/checks.py
```

The two score commands check method digests, prediction receipts and reference hashes. They print counts for each sample. The agreement command compares two independent annotations of the 32-call session. The checks use PowerShell 7 to verify command parsing and context timing.

## Inspect the evidence

- `evidence.py` reads the Git tree at the starting revision and calculates a small set of file, script and result markers. It counts file kinds and checks for topic terms in the historical README locally.
- `syntax.ps1` parses each recorded command as text. It never invokes the command. It returns syntax nodes and literal operands for supported commands.
- `method.py` classifies literal operands, combines independent reads, reuses earlier file roles, and prepares Luna input for unresolved calls. Both methods use the same Luna instructions and settings.
- `prepare.py` prepares inputs for the additional sessions and removes identifying absolute paths and output text. The earlier set uses its saved baseline inputs. Context fields contain structural facts.
- `study/observations.zip` contains the four label-free inputs and their precomputed structural evidence. Each member matches the digest in `study/freeze.json`. The two development references are separate JSON files. Keep both blind references sealed. Use other sessions for future development.
- `study/receipt.json` pins the predictions saved before scoring. `study/scores.json` gives aggregate pairs, targets and actions, the number of corrected and newly wrong pairs, and the number and accuracy of local decisions.
- `literal.py` keeps the previous decisions for unresolved calls. It recognizes supported literal operands and bounded Python file operations. `literal-study/` holds a separate method freeze, paired predictions and the 57-call blind set used for this trial. That set uses de-identified commands checked by its independent reviewer.

The disk check for a checkout root uses the filesystem at inspection time. The other project facts use the recorded starting revision. A file-kind marker describes a possible operand. Only the recorded command and its result can establish an operation. Some requests contain data or execution work for which the target taxonomy has no precise row.
