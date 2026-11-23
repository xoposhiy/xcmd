"""Refresh this study's section in the existing self-contained HTML report."""
import json
import re
from html import escape
from pathlib import Path

D=Path(__file__).resolve().parent
def load(name):
    return json.loads((D/name).read_text(encoding="utf8"))
def pct(x):
    return "—" if x is None else f"{100*x:.1f}%"
def labels(pairs):
    names={"source":"Application source","tests":"Test files","docs":"Documentation & instructions",
           "tooling":"Project tooling","git":"Git","tracker":"Issue tracker","mixed":"Mixed or project-wide",
           "project":"Project-wide","unknown":"Unknown"}
    parts=[p.split("/") for p in sorted(pairs)]
    return ", ".join(escape(names[t]+" / "+a.capitalize()) for t,a in parts)
def tr(values):
    return "<tr><th scope=\"row\">"+escape(str(values[0]))+"</th>"+"".join("<td>"+escape(str(v))+"</td>" for v in values[1:])+"</tr>"
def table(headers,rows):
    return '<div class="panel score-shell"><table class="scores"><thead><tr>'+''.join("<th>"+escape(h)+"</th>" for h in headers)+'</tr></thead><tbody>'+''.join(tr(r) for r in rows)+'</tbody></table></div>'

s=load("scores.json");audit=load("historical-audit.json");ex=load("development-examples.json")
main=s["full_session"];challenge=s["development_challenge"];model=s["luna"];c=audit["primary_cause_among_disputes"]
score_table=table(["Sample","Calls","Single pair exact","Multi-pair set exact","Mean Jaccard","Micro-F1"],
    [(title,x["n"],x["single_exact"],x["multi_exact"],pct(x["mean_jaccard"]),pct(x["micro_f1"])) for title,x in
    [("Same full session",main),("Historically disputed calls",s["historical_conflicts"]),("Separate development challenge",challenge)]])
cause_table=table(["Auditor's primary explanation","Disputed calls"],[(k.capitalize(),v) for k,v in sorted(c.items(),key=lambda kv:-kv[1]) if v])
model_table=table(["Comparison","Calls","Exact sets","Mean Jaccard","Micro-precision","Micro-recall","Micro-F1"],
    [(title,x["n"],x["exact"],pct(x["mean_jaccard"]),pct(x["micro_precision"]),pct(x["micro_recall"]),pct(x["micro_f1"]))
      for title,x in [("Against reviewer A",model["against_a"]),("Against reviewer B",model["against_b"]),("Identical reviewer sets only",model["agreed_calls"])] if x["n"]])
examples=[]
context_block=""
if (D/"context-scores.json").exists():
    result=load("context-scores.json")
    comparison=table(["Input","Exact reviewer sets","Reviewer Jaccard","Jaccard against frozen full reviews"],
            [(label, str(result[k]["exact"])+"/32", pct(result[k]["mean_jaccard"]),
              pct(result[k]["mean_jaccard_vs_frozen_full_reviews"]))
             for k,label in [("short","Compact call"),("full","Full trace and project files")]])
    context_block=f'''<details open id="review-context"><summary>What happened when reviewers received the full trace?</summary>
<p>Two additional reviewers labelled all 32 calls twice with the same multi-pair guide. The compact packets had commands, working directories, status and file-change metadata. The output excerpts were empty. Both first passes were sealed before either reviewer got the full session, correlated results and permission to inspect the project.</p>
{comparison}
<p>After the second pass the two reviewers changed {result["changed_c"]} and {result["changed_d"]} sets. The final column averages all four comparisons with the two frozen full-context reviews from the paired study. It measures agreement with those reviewers. Extra context and taking a second look occur together in this design.</p></details>'''
for k,title in [("point-taken-L31","One read command, two file roles"),("point-taken-L146","Staging and running a deployment")]:
    e=ex[k]
    examples.append('<details id="example-'+k+'"><summary>'+title+'</summary><pre class="source">'+escape(e["command"])+'</pre><p>Single pair · A: '+labels([e["a1"]])+' · B: '+labels([e["b1"]])+'</p><p>All supported pairs · A: '+labels(e["a2"])+'<br>All supported pairs · B: '+labels(e["b2"])+'</p></details>')
text=f'''<section id="multilabel"><h2>Why did the reviewers disagree?</h2>
<p>An independent auditor revisited all 15 disputed calls in the original 32-call session. Seven calls clearly contained several observable pairs. Recording all pairs fully accounted for two disagreements, partly accounted for two and left eleven unexplained by multiplicity. These are retrospective judgments from one auditor.</p>
{cause_table}
<p class="legend">Both historical reviewers could inspect the full local trace and project files. All 15 disputed records contained a command, working directory, status and output. The record cannot establish which cues each reviewer noticed. Some grouped outputs were truncated. The audit found no confirmed unequal access to material evidence.</p>
{context_block}
<h3>Same context, two annotation formats</h3>
<p>Two new independent Codex reviewers first sealed a single target and action for every call. Both then recorded sets of supported target/action pairs. Each had the same trace and repository access in both passes. The original review had 17 exact matches in 32 calls. The separate eight-call sample deliberately includes compound operations and cannot estimate their overall frequency.</p>
{score_table}
<p class="examples">On the full session, reviewers A and B recorded {main["a_pairs"]} and {main["b_pairs"]} pairs. They marked {main["a_multi"]} and {main["b_multi"]} calls as multi-pair, both doing so on {main["both_multi"]}. Moving to sets gained {main["gained_exact"]} exact matches and lost {main["lost_exact"]}. Different reviewers can also vary on a repeated single-pair task. Fixed pass order limits any causal interpretation.</p>
<details id="paired-metrics"><summary>What do the agreement scores count?</summary><p>Exact agreement requires identical sets. Per-call Jaccard is the size of the shared set divided by the size of the union. Average it over calls so a long script has the same weight as a simple command. Micro-F1 pools pair overlap across calls, giving calls with more observations more influence.</p>
<p>A reference with <code>source/read</code> and <code>docs/write</code> and a prediction with only <code>source/read</code> have Jaccard 0.50, precision 1.00, recall 0.50 and F1 0.67. Swapping the actions between the resources yields zero pair overlap.</p>
<p>Either reviewer marked {main["multi_subset_n"]} full-session calls multi-pair. Their mean Jaccard was {pct(main["multi_subset_jaccard"])}. The reviewers agreed on the set of targets for {main["multi_target_exact"]} calls and the set of actions for {main["multi_action_exact"]}. Unknown-axis pairs occurred in {main["a_unknown"]} and {main["b_unknown"]} calls. Dropping them leaves {main["known_union_n"]} calls with a nonempty union and Jaccard {pct(main["known_only_jaccard"])}.</p>
<p>Collapsing both sets to a single pair was defined for {main["collapse_eligible"]} calls. A collapsed match hid a difference between the underlying sets on {main["collapse_hidden_diff"]} calls.</p></details>
<h3>What to ask the annotator</h3>
<p>Identify each observed operation, the resource it directly affected and the evidence. Assign its project role after identifying the resource. Keep the command or tool in a separate field. List uncertain alternative roles separately. A repository-wide list can have the target Project-wide. A read of both source and tests has two supported pairs. A failed query can have a known operation and an unresolved resource.</p>
{''.join(examples)}
<details id="multi-model"><summary>How Luna scored against the two independent reviews</summary>
<p>Luna received the previously approved compact call packets. The reviewers had full trace access. Each positive pair needed an evidence cue. The original labels stayed sealed until the predictions were fixed. Agreement with one reviewer is a comparison with that reviewer; the unanimous subset excludes genuinely disputed calls.</p>
{model_table}
<p>Each call can contribute to several cells. For a chart that must sum to the number of calls, give each of a call's k distinct pairs a display weight of 1/k. This describes chart allocation. Probability estimates require separate calibration.</p></details>
<p class="legend"><a href="../../tests/SessionClassification/activity-target-study/multilabel/protocol.md">Study protocol</a> · <a href="../../tests/SessionClassification/activity-target-study/multilabel/luna-prompt.txt">Exact Luna prompt</a> · <a href="../../tests/SessionClassification/activity-target-study/multilabel/historical-audit.json">Aggregate audit</a> · <a href="../../tests/SessionClassification/activity-target-study/multilabel/scores.json">All measurements</a></p></section>'''
path=D.parents[3]/"docs/reviews/activity-target-experiment.html"
html=path.read_text(encoding="utf8")
if '<section id="multilabel">' in html:
    html=re.sub(r'<section id="multilabel">.*?</section>',lambda _:text,html,count=1,flags=re.S)
else:
    html=html.replace('<section id="agreement">',text+'\n<section id="agreement">',1)
    html=html.replace('<a href="#agreement">Reviewer agreement</a>','<a href="#multilabel">Disputed labels</a><a href="#agreement">Earlier agreement</a>',1)
if '<section id="multilabel">' not in html:raise ValueError("Missing study section")
path.write_text(html,encoding="utf8",newline="\n")
