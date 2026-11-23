# Single-pair and multi-pair agreement

The [HTML report](../../../../docs/reviews/activity-target-experiment.html#multilabel) describes the disagreement audit, the paired annotation experiment and the context follow-up.

Two new reviewers labelled the same 32-call session and eight deliberately chosen development calls in two passes. Their first responses were sealed before they learned the multi-pair guide. Two additional reviewers then labelled the 32-call session first with compact inputs and then with full trace access, using the same multi-pair guide in both passes. The old references remain unchanged.

Read [the annotation protocol](protocol.md), [the context protocol](context-protocol.md), [the historical disagreement audit](historical-audit.json), and [Luna's instructions](luna-prompt.txt). The eight-call development sample supports inspection of concrete cases. Individual blind annotations are reserved for evaluation.

From the repository root, reproduce the aggregate comparisons without a network request:

```
python -B tests/SessionClassification/activity-target-study/multilabel/test_scores.py
python -B tests/SessionClassification/activity-target-study/multilabel/score.py
python -B tests/SessionClassification/activity-target-study/multilabel/context_scores.py
```

The scoring programs verify input receipts and reject a changed published score. Mean Jaccard gives every call equal weight. Micro-F1 pools pair counts. Exact set agreement requires every pair to match. Unknown-axis pairs are included in the main results. A sensitivity measure excludes them. Each Luna score is relative to a specified reviewer. Reviewer agreement is measured separately.
