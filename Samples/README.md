# Samples

## RuntimeAssets

`RuntimeAssetProviderSample` shows how the client can hide the selected runtime asset backend behind one API. The original portfolio build uses Addressables, while the code structure keeps resource loading logic isolated from gameplay and UI code.

## Loading

`LoadingProgressPresenterSample` shows a rough but smooth loading progress UI that separates actual work stages from visible slider interpolation.

## Skills

The skill samples show how hit detection and damage calculation can be centralized so individual skills do not duplicate damage rules.

## Optimization

`EnemySeparationNonAllocSample` demonstrates a mobile-friendly physics query pattern: static buffers, staggered checks, and no per-frame `OverlapCircleAll` allocation.

## EditorTools

`AndroidBuildPipelineSample` shows build automation concepts: define symbols, version increment, APK/AAB switching, enabled scene collection, and build failure rollback.

