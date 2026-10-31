# LumiWorld Review 2 diagram sources

Project FA26SE18. Draft prepared 28 October 2026 for Review 2 on 5 November 2026.

These editable Mermaid files belong to Report 4. Captions in the report and manifest distinguish target models, observed source and pending design. They are not evidence of passing tests. ERD, deployment details and final gameplay/UI figures are still required.

The Markdown report embeds the same sources for reading. Keep the `.mmd` files and report blocks synchronized. Export and visually check every diagram before the submission PDF; source review alone does not verify rendered layout.

| ID / source | Diagram | Trace |
| --- | --- | --- |
| [SYS-01](SYS-01.mmd) | System overview and authority boundaries | FR-1, FR-10 to FR-15; design sections 11-15 |
| [GAME-01](GAME-01.mmd) | Core play cycle | FR-2 to FR-9, FR-16 to FR-18; design sections 2-9 |
| [ACT-01](ACT-01.mmd) | Establish the first functional biome | WF-01; FR-1, FR-2, FR-3, FR-15 |
| [ACT-02](ACT-02.mmd) | Produce, craft and restore creature contribution | WF-02; FR-4, FR-5, FR-6 |
| [ACT-03](ACT-03.mmd) | Orders, milestones and island expansion | WF-03; FR-7, FR-8 |
| [ACT-04](ACT-04.mmd) | Authoritative resource or Creature Card trade | WF-04; FR-11, FR-12; design section 13 |
| [ACT-05](ACT-05.mmd) | Publish, visit and handle a delayed obstacle | WF-05; FR-9, FR-10, FR-12 |
| [ACT-06](ACT-06.mmd) | Inspect and tame a wild Alpha | WF-06; FR-16 |
| [ACT-07](ACT-07.mmd) | River topology, scoring and persistence | WF-07; FR-17, FR-1, FR-2 |
| [ACT-08](ACT-08.mmd) | Authorized administration | FR-14, FR-15; design section 14 |
| [ACT-09](ACT-09.mmd) | Breed eligible parents and claim one offspring | WF-08; FR-18, FR-4, FR-1; FR-11 lock interaction |
| [STATE-01](STATE-01.mmd) | Account eligibility | FR-1, FR-14, FR-15 |
| [STATE-02](STATE-02.mmd) | Local and cloud save synchronization | FR-1 |
| [STATE-03](STATE-03.mmd) | Biome structure and usable habitat | FR-2, FR-3, FR-9 |
| [STATE-04](STATE-04.mmd) | Creature ownership and contribution | FR-3, FR-4, FR-9, FR-11, FR-18 |
| [STATE-05](STATE-05.mmd) | Craft job lifecycle | FR-6 |
| [STATE-06](STATE-06.mmd) | Market listing lifecycle | FR-11, FR-12 |
| [STATE-07](STATE-07.mmd) | Inbox handling and acknowledgement | FR-12; design section 13 |
| [STATE-08](STATE-08.mmd) | Incident lifecycle and persistent biome damage | FR-9, FR-12 |
| [STATE-09](STATE-09.mmd) | Wild encounter and taming attempt | FR-16 |
| [STATE-10](STATE-10.mmd) | Breeding job, parent release and offspring claim | FR-18; ACT-09 |
| [STATE-11](STATE-11.mmd) | Creature ownership and incompatible-use locks | FR-4, FR-11, FR-18; BR-13, BR-14 |

## Required follow-up

- Supply the logical ERD and matching scripts, including planned server asset authority.
- Review candidate workflows and assign one owner per selected workflow; at least five qualifying workflows are required for this team.
- Confirm pending gameplay/account policies before treating diagram branches as a signed baseline.
- Derive transition tests, including invalid transitions; count these only after the main-entity set is approved.
- Render exports with readable labels and captions for the English submission PDF.

## Transition checks to derive

| State source | Important rejected or duplicate event |
| --- | --- |
| STATE-01 | Invalid registration; protected request from suspended account |
| STATE-02 | Stale upload; failed reload; edits arriving during upload |
| STATE-03 | Mixed Nature/height footprint; invalid capacity or repeated charge |
| STATE-04 | Invalid placement; treating hunger while sickness still blocks work |
| STATE-05 | Invalid recipe; repeated delivery after output was awarded |
| STATE-06 | Buy/cancel/expiry race; new purchase on closed listing |
| STATE-07 | Duplicate fetch, crash around durable apply/ack, restore on another device |
| STATE-08 | Offline catch-up; fire expiry incorrectly restoring burned tiles |
| STATE-09 | Visitor taming; timeout award; repeated completion callback |
| STATE-10 | Same parent twice; locked parent; duplicate payment/release/claim after restore |
| STATE-11 | Listing vs breeding race; seller access after transfer; out-of-range creature price |

No runtime check or diagram render result is implied by this list.
