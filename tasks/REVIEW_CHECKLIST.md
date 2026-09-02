# Human Review Checklist

Use this before accepting a coding-agent task.

## 1. Scope

- [ ] The change addresses only the selected task.
- [ ] No follow-on feature, speculative abstraction, or unrelated cleanup was included.
- [ ] No user work was deleted, reset, reformatted, or reorganised without need.
- [ ] New files and dependencies are explicitly permitted by the task.

## 2. Architecture

- [ ] Dependency direction complies with ADR-0002.
- [ ] `CommandoWar.Sim` contains no host, editor, rendering, audio, platform, or wall-clock dependency.
- [ ] Authoritative state changes only in the simulation step and documented phases.
- [ ] Framework-specific types stop at an adapter or import boundary.
- [ ] The change did not create a permanent abstraction over competing spike frameworks.

## 3. Determinism and correctness

- [ ] Authoritative time uses integer ticks.
- [ ] Randomness uses the project-owned source.
- [ ] Collection iteration is stable where order affects results.
- [ ] No frame delta, thread scheduling, hash enumeration, or framework physics affects outcomes.
- [ ] Invalid state or input fails explicitly.
- [ ] Behavioural changes have focused tests.
- [ ] Replay or state-hash consequences were checked when relevant.

## 4. Maintainability

- [ ] Names describe domain responsibility rather than implementation fashion.
- [ ] Types reduce actual invalid states without speculative machinery.
- [ ] System order and ownership are visible.
- [ ] Error messages identify the failing item and expected correction.
- [ ] Comments explain constraints or reasons, not obvious syntax.
- [ ] Performance complexity is justified by evidence.

## 5. Verification evidence

- [ ] Exact commands are reported.
- [ ] Relevant tests passed or failures are honestly documented.
- [ ] A build or runnable smoke test was performed where practical.
- [ ] Manual claims have reproducible steps.
- [ ] Benchmarks include configuration and environment when required.
- [ ] No disabled, weakened, ignored, or flaky test conceals a failure.

## 6. Task acceptance

- [ ] Every acceptance criterion maps to evidence.
- [ ] Deviations are explicit and acceptable.
- [ ] The task file has the correct final status.
- [ ] `docs/11_BACKLOG.md` is consistent.
- [ ] `docs/12_PROGRESS_LEDGER.md` records the work and evidence.
- [ ] `PROJECT_STATE.yaml` names the correct next state without activating unapproved work.
- [ ] ADRs and design documents contain no unresolved contradiction introduced by the change.

## 7. Decision

Choose one:

- `accept`: all material criteria are satisfied.
- `request focused changes`: list only task-related deficiencies.
- `reject`: architecture, evidence, or scope is materially unsound.

Do not accept a change because repairing it later appears easy. The documentation and evidence are part of the task.
