# 14. Playtest kit (P5 preparation)

Realises backlog B-036 (TASK-080). Read with `docs/07_VERTICAL_SLICE.md`
section 11 (the thresholds and what to record) and `docs/08_ROADMAP_AND_GATES.md`
section 8 (P5 required work and G5 evidence). This is a product heuristic
exercise with at least five non-developers, not a formal study, and must not
be described as one.

## 1. What is prepared, and what is not

Prepared and verified in the build environment (no Godot install, no
external network):

- per-launch session recording in the play scene (section 3);
- a headless reader that shows each order's disposition and refusal reasons
  from a recorded session (section 3);
- the facilitator script, participant controls card, observation sheet, and
  issue form (sections 4 to 7).

Prepared but **not verified**, because they need the Godot editor and export
templates:

- the exported distributable itself. No binary was produced. The procedure in
  section 2 is what an operator with Godot runs; its checklist is the
  acceptance test for the build.
- the host change that finds `content/` beside an exported executable
  (`FSharpSceneHost.ResolveContentPath`). It compiles only under the Godot SDK.

Not in scope here, recorded as findings in section 8: the HUD's developer
line, the window title, replay playback of session files inside the client.

## 2. Build procedure (Windows desktop)

Godot .NET 4.7.x with the matching export templates installed (the project
declares feature `4.7`; `docs/02_TECHNOLOGY_DECISION.md` records 4.7.2). The
templates were **not installed** on the developer machine when TASK-004
attempted a packaged export (`docs/ledger/2026-09-02-TASK-004-godot-spike.md`;
`src/CommandoWar.Client.Godot/README.md`, "Packaging"): in the editor, Editor,
Manage Export Templates, install `4.7.2.stable.mono` first, and re-check that
this is still true. The repository commits no `export_presets.cfg`; create the
preset in the editor and do not commit it unless Dave decides to.

1. From a clean checkout of the commit under test, build the solution:
   `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Release`.
2. In the Godot editor open `src/CommandoWar.Client.Godot`, then Project,
   Export, add a `Windows Desktop` preset, turn off "Export With Debug", and
   export to `src/CommandoWar.Client.Godot/export/` (already git-ignored).
   Godot writes the executable and a `data_*` folder; keep them together.
3. Copy `content/scenarios/bridgehead.cwscenario` from the repository to
   `content/scenarios/bridgehead.cwscenario` **beside the executable**. The
   host looks there first and falls back to the repository path only when the
   file is absent, so an exported build without this copy fails to load the
   mission. The main scene (`CommandDemo.tscn`) needs nothing else from
   `content/`; the replay viewer scene is not part of the playtest.
4. Record the commit hash and the build date in a `BUILD.txt` beside the
   executable. The session file's `build` line carries only the assembly
   version, so this file is the link from a session back to a commit.
5. Zip the folder. Do not include the repository, `.godot/`, or `art/`
   sources.

Acceptance checklist for the build (run on a machine that has never had the
repository or a .NET SDK):

- [ ] Double-clicking the executable opens the Bridgehead mission with no
      command line, no console window, and no developer flag.
- [ ] The HUD shows no `SESSION RECORDING FAILED` text.
- [ ] A file `session-<UTC time>.cwreplay` exists in the folder named in
      section 3 within a second of launch.
- [ ] Issuing one order and pausing with `Space` updates that file's `ticks`
      line to the current tick.
- [ ] Playing to `Succeeded` or `Failed` shows the mission summary panel and
      leaves a final session file whose first line reads `# playtest session:
      mission outcome ...`.
- [ ] The executable, run from a folder outside the repository, behaves
      identically (this is what proves the content lookup works).
- [ ] `cwheadless replay-file <that file>` reproduces the tick count and
      final hash the run ended on (section 3).

## 3. Session recording and analysis

Every launch of the play scene writes one session file, rewritten in full:

- when an order is delivered to the simulation;
- every 20 ticks (one second) of play;
- whenever the run pauses (`Space`, the mission ending, or the automatic
  pause when an agent refuses an order);
- when the mission ends.

A session closed without pausing therefore loses at most the last second.
Location: `CommandoWar/playtest/` under the per-user application-data folder,
that is `%APPDATA%\CommandoWar\playtest\` on Windows and
`~/.config/CommandoWar/playtest/` on Linux (macOS not checked). The file is the
production replay-command format (`docs/04_SIMULATION_SPEC.md` section 16): the
accepted command stream plus seed, scenario label `bridgehead`, and the
initial-state hash. It contains no name, clock time beyond the file name, or
other personal data. If the write fails the HUD says so on its status line; the
run is not stopped.

Restart count is the number of session files a participant produces in one
sitting; the observation sheet also records it by hand.

To read a session (needs the .NET SDK and this repository; this is the
facilitator's or analyst's step, not the participant's):

```
dotnet run --project src/CommandoWar.Headless -c Release -- replay-file <session.cwreplay>
```

It replays the file, checks the initial-state hash, and prints the accepted
commands, the per-tick state hashes, the final agents, then an `order
appraisals` section: one line per appraisal with tick, agent, command id, and
the disposition. A refusal prints the typed reason, for example
`Refused (RouteTooExposed (Some AgentId 102), [||])`, which names the threat
the agent would not walk toward. A last line gives the mission outcome. This is
the evidence for "identify why an agent resisted from recorded evidence"
(G5). It does not render map frames; that would be a follow-on if analysis
needs it.

The replay is deterministic: the tick count and final hash printed are the
ones the client held when the file was last written. A mismatch means a
different build wrote the file than the one reading it (check `BUILD.txt`).

## 4. Facilitator script

Roles: one facilitator, one silent observer if available. No developer
coaching: the facilitator never explains a refusal, a control, or what to do.

Before the participant arrives: fresh build unzipped, game closed, screen
recording (with consent) and the observation sheet ready. Note the time.

Read aloud, without adding to it:

> Thanks for helping. We are testing the game, not you; nothing you do is a
> wrong answer. You will command a small squad on one mission. I will not be
> able to answer questions about how the game works while you play, but I will
> write down anything that confuses you, and that is useful. Please say out
> loud what you are thinking and what you expect to happen. You can stop at
> any time.

Give the controls card (section 5) and the mission line:

> Your squad has to destroy the bridge charge and then get everyone out to the
> extraction point. There are enemy soldiers.

Then start the game and say nothing further. Let them play for up to 30
minutes or until the mission ends, whichever is first. If they finish, ask
whether they want to play again and note the answer; do not encourage it.

Allowed prompts when a participant is silent or stuck for over a minute, in
this order, one at a time:

1. "What are you thinking right now?"
2. "What did you expect to happen?"
3. "What would you try next?"

Never say: why an agent did or did not obey, what a symbol or message means,
which agent to choose, or that refusal is a feature. If a participant asks, say
"I can't say; what do you think it means?" and record the question.

Stop rules: stop at once if the participant is distressed, or on a crash or
data-loss defect (record it as blocker on the issue form). A participant who
cannot issue a move order after five minutes is recorded as a control failure
and given the first move order by the facilitator so the session can continue.

After play, ask these questions in this order, recording answers verbatim:

1. "In your own words, what happened when an order didn't go as you wanted?"
   Then, if they saw a refusal: "Why do you think that soldier did that?"
2. "Was there anything you could have done to change that? What?"
3. "When you changed something on the map, or gave a different order, did the
   result match what you expected?"
4. "Was there a moment that felt unfair or random? What was it?"
5. "Did any control or order do something you didn't expect?"
6. "Did the soldiers' behaviour feel meaningful, arbitrary, or just for show?"
7. "Would you play this mission again on your own? Why or why not?"

Finish by saving the participant's session file(s) under the participant code
(`P1` to `P5`, never a name) and noting the file names on the sheet.

## 5. Participant controls card

Give this card as printed text and nothing more. It lists inputs only; it
deliberately says nothing about how soldiers respond to orders.

```
Left-click a soldier ........ select it
Left-click a map cell ....... order the selected soldier there
Drag a box .................. select every soldier inside it
Shift + left-click .......... add a soldier to, or remove it from, the selection
Right-click ................. clear the selection
Space ....................... pause / resume
Icons, bottom-left .......... choose a different kind of order, then click a cell
```

Not on the card: `F1` (developer overlay). If a participant finds it, record
that and what they did with it; do not remove it from the build for this test
without a decision from Dave.

## 6. Observation sheet (one per participant)

Header: participant code, date, build (`BUILD.txt` hash), facilitator,
observer, session file names.

| Item (docs/07 section 11) | Record |
|---|---|
| Understood why an agent resisted, unaided (yes / partly / no, quote) | |
| Named an action that could change the decision, unaided (yes / no, quote) | |
| Changed the battlefield or order and got the expected result (yes / no / not attempted) | |
| Mission completed (Succeeded / Failed / abandoned), time played | |
| Restart count | |
| Moments of surprise or perceived unfairness (time, what) | |
| Commands or symbols misunderstood (which, how) | |
| Autonomy felt: meaningful / arbitrary / cosmetic (quote) | |
| Would voluntarily replay (yes / no, why) | |
| Used `F1` overlay (yes / no, what for) | |
| Coaching given (must be "none"; list any slip) | |

Continuation signal, tallied across five sessions from this sheet
(`docs/07` section 11): at least four of five explain the principal refusal
reason unaided; at least four name a corrective action; at least three
voluntarily complete or replay; no recurring sign that refusal reads as random
or as input failure; deterministic replay still intact (each session file
replays).

## 7. Issue form

One entry per issue, whoever finds it.

| Field | Value |
|---|---|
| ID | ISS-nnn |
| Participant / build | P#, build hash |
| Time in session and what the participant was doing | |
| What happened (observed, not interpreted) | |
| What the participant said or expected | |
| Category | comprehension, control, balance, presentation, technical |
| Severity | blocker (crash, data loss, cannot proceed), major (recurs or blocks a goal), minor |
| Recurrence | count of participants affected so far |
| Session file and tick, if identifiable | |

Categories keep comprehension problems apart from balance and presentation
problems (P5 required work). A refusal the participant misread is
comprehension; a refusal the participant understood but found too punishing is
balance; a marker that could not be seen is presentation.

## 8. Findings to decide before session 1

Found while preparing the build; none changed here.

1. **The HUD status line is developer-oriented.** It always shows `tick`,
   the 64-bit state hash, `draws`, and agent count. It may distract testers or
   invite questions the facilitator must not answer. Presentation decision for
   Dave: leave, or hide behind `F1` for the test build.
2. **The window and project title read "CommandoWar Godot Spike".** Public
   title and branding are out of scope (`AGENTS.md`); it is visible to
   participants.
3. **The client cannot open a session file.** Analysis needs the .NET SDK and
   this repository. Replaying a session visually inside the client is a
   separate task if the analyst needs it.
4. **No in-game restart.** A participant who wants to retry closes and
   relaunches, which is why a session file is per launch.
5. **The host lookup change and the export are unverified** (section 1).
6. **The developer overlay (`F1`) is reachable by any participant.**

## 9. Mapping to G5 evidence

| G5 evidence (docs/08 section 8) | Where it comes from |
|---|---|
| continuation thresholds met | section 6 tally |
| recurring failures and fixes documented | section 7 log, then the P5 ledger entries |
| why an agent resisted, from recorded evidence | section 3 `order appraisals` |
| testers act on explanations without coaching | section 6 "coaching given" and the unaided-action rows |
| no critical accessibility, control, or data-loss defect | section 7 severities; section 2 checklist |
