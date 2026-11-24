# Session inspector API cost timeline

Agreed and implemented on 2026-11-24.

Show where a session spent its model resources. Use the same standard API rates
to compare the cost of different responses within a session.
[InspectAgentSession](../stories/coding-agents.md#inspectagentsession)

## Axes

- Replace `Est. tokens` with `API cost`. Open the inspector on `API cost`, even
  when some or all responses have no cost data. Keep `Time` available.
- Give each model response one interval on a common cost axis. Use its
  calculated API cost in USD for the width. Count the response once in the
  selected range and whole-session cost totals.
- Use recorded tool call intervals on the `Time` axis so the user can see when
  calls ran, including calls that ran in parallel.

## Usage and prices

- Use token counts reported in the session transcript. Use one usage record per
  model response. In Codex, identify the response by its thread and response ID.
  In Claude Code, count usage once per message ID, even when the transcript
  stores several records for the same message.
- Apply the standard global API rates for the model that produced each response.
  Use the same tariff snapshot for all sessions, including older sessions and
  sessions run under a subscription or in Fast mode. Show the rate snapshot date
  in the cost tooltip. The snapshot is dated 2026-11-24.
  [Session API prices](../../src/Xcmd.Desktop/config/session-api-prices.json)
- Price uncached input, cache reads, cache writes and output at their respective
  rates. Apply the input length and cache duration rules from the price file.
  Use each provider's usage fields to derive the categories. OpenAI's input
  count includes cache reads and writes. Claude reports them separately.
- Reasoning and thinking tokens are part of the response's output tokens. Charge
  for output once. Show reasoning as part of the response's cost details.
- Associate a response's cost with the calls emitted in that response. Include
  the cost of responses with zero calls, such as plans, progress messages and
  final answers.

## Main timeline

- Draw the full interval of a response on each target track touched by its tool
  calls. For example, an answer with one `Source` call and one `Tests` call
  appears at the same position and width on both tracks.
- When showing several calls sequentially inside a response on one track, divide
  that track's interval into equal-width call segments. Put the segments in the
  order in which the model emitted the calls.
- Show responses without calls in the agent's `Others` track with a distinct
  color. Use `Messages` for a group of adjacent responses without calls.
- Join the fill of adjacent responses for the same agent and target within the
  same user task. Join across `Work phases` boundaries. Apply this rule on each
  track independently. For example, if response 1 touches `Source`, response 2
  touches both `Source` and `Tests`, and response 3 touches `Tests`, join
  responses 1 and 2 on `Source` and responses 2 and 3 on `Tests`.
- Show a block of fixed display width with a red outline when cost data is
  missing. Keep it in its place in the response order and exclude it from
  monetary totals. Show the reason in the tooltip. Use `Usage unavailable`
  when the usage record is missing.

## Touched resources

- Use the same cost axis, response intervals and joining rules as the main
  timeline. Draw the full interval on each resource row touched by a call in
  the response. For example, if a response calls tools for two files, both file
  rows show the same full interval.
- Join adjacent intervals on each resource row within the same user task and
  agent. Keep the individual response boundaries available for hover and click.

## Hover, selection and range

- On hover, reveal the boundaries of the response under the pointer on all rows
  that show that response, in both timelines. This includes boundaries hidden
  by joined fills.
- A click in either timeline selects the same model response and opens its cost
  and all its calls. Focus the details on the calls for the clicked target or
  resource.
- Snap the range handles on the cost axis to response boundaries, including
  boundaries inside a joined block. Sum the costs of whole responses in the
  selected range.

## Activity breakdown

- Split a response's cost equally among all its calls. A response costing
  $0.06 with one `Source` call and two `Tests` calls adds $0.02 to `Source`
  and $0.04 to `Tests`.
- If a call has several targets, split its share equally among those targets.
  When showing actions separately, split each target's share equally among its
  actions. Totals for the same calls match in both views.
- Round only for display. Use the full response and call shares when summing
  totals.
