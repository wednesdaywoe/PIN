# PIN

## Plans

This project's plans and tracked items are in `worklog.jsonl`, one change per
line, added and never edited. Read the plan before touching the code it
governs: `worklog show` lists the plans, `worklog show <plan>` prints one.
Change the file only with `worklog write '<change as JSON>'`, never by hand;
it refuses what the rules do not allow and says why. Items moved from the old
Markdown keep their old ids (`NET-23`, `GRADE1`) as `was`, which
`worklog search` finds.

Each issue's full story is in `Docs/gaps/`, one section per id (`data-23`,
`net-18`, `client-4`). In-game test runs are written into
`Game Testing/test-run.html`.
