# Architecture Overview

## Portfolio Build Strategy

The original project uses Unity URP and Addressables for a portfolio-friendly resource structure. The client-side code is organized so loading, data preparation, lobby entry, and runtime skill setup are visible as separate stages.

```text
App Start
  -> Login / Server Data
  -> Sheet Data
  -> Runtime Assets
  -> Lobby
  -> Stage
```

## Why These Samples Exist

The commercial project cannot be published as-is, so this repository shows public-safe samples of the important parts:

- How resource loading is wrapped behind one provider
- How loading progress is smoothed
- How skill damage is centralized
- How runtime skills avoid duplicating collision/damage logic
- How mobile physics queries reduce GC allocation
- How build settings are automated from Unity editor menus

## Portfolio Presentation Split

- PDF/PPT: short, fast to scan
- Notion: detailed project stories with videos and screenshots
- GitHub: code credibility and architecture examples

