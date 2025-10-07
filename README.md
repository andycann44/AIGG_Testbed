# AIGG Testbed

**Goal:** NL → local intents/macros → canonical JSON → Paste & Merge → track build (no OpenAI fallback yet)

## What’s wired
- WorkbenchWindow (Editor):
  - Top = NL input
  - Middle = Diagnostics (normalized + matched by source + unmatched chips)
  - Bottom = JSON output (when parsers succeed)
  - Buttons: Parse NL (local), Open Paste & Merge, Copy skeleton intent
- Local-only parsing order:
  1) NLToJson.GenerateFromPrompt(string)
  2) fallback AIGG_NLInterpreter.RunToJson(string) or .Run(string)
- Spec source: `Assets/AIGG/Spec/*.json` (fallback: `Resources/Spec/*`)
- Settings: AIGG_Settings.asset created locally (API key NOT in repo)
- IntentRunner located at `Assets/AIGG/Editor/Local/IntentRunner.cs`

## How to use
1) Unity → Window → Aim2Pro → Workbench
2) Type NL (e.g., `straight 100m, right 90°, gap 5m, width 3m`)
3) Parse NL:
   - If JSON appears: Open Paste & Merge
   - If not: pick an Unmatched chip → Copy skeleton intent → paste into Paste & Merge → save → retry

## Next steps
- Better unmatched suggestions (prefilled command blocks)
- Hook Track Builder to NL→JSON→Merge flow
- Optional: OpenAI fallback toggle (later)

