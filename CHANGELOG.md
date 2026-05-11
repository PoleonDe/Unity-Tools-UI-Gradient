# Changelog

All notable changes to this package will be documented in this file.

## [0.3.0] - 2026-05-11

### Added
- Added `Control/UI Soft Mask` and `Control/UI Soft Maskable` components for gradient alpha masking.
- Added `Control/UI/SoftMask` shader support for default Unity UI graphics.
- Added soft-mask sampling support to `Control/UI/Gradient` so gradients can be masked by other soft masks.
- Added a `Show Mask Graphic` toggle to `Control/UI Soft Mask`.

### Changed
- Exposed runtime gradient alpha evaluation for generated soft-mask textures.
- Soft mask texture resolution is now calculated automatically from the mask rect and canvas scale.

## [0.2.1] - 2026-05-11

### Changed
- Updated gradient editor number controls so the left icon area drags values while the number field remains directly editable.
- Added a horizontal resize cursor over draggable number-control handles.

## [0.2.0] - 2026-05-11

### Added
- UI Toolkit gradient editor window with preview, draggable stops, alpha editing, and SVG icon assets.
- Runtime gradient texture upload so multi-stop gradients render accurately in the UI shader.

### Changed
- Updated rotation and squash-handle behavior to preserve gradient geometry more consistently.
- Updated the shader/material to sample `_GradientTex` while keeping the package shader path as `Control/UI/Gradient`.

## [0.1.0] - 2026-05-10

### Added
- Initial UI Gradient runtime component, editor handles, and shader.
- Resources material reference to keep the gradient shader available in player builds.

### Changed
- Renamed the shader path to `Control/UI/Gradient`.
