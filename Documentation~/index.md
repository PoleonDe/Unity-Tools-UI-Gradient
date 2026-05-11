# Control Tools - UI Gradient

`Control.Gradient` is a `MaskableGraphic` for drawing configurable gradients in Unity UI.

## Features

- Linear, radial, angular, and diamond gradient modes.
- Multi-stop color and alpha editing through a custom UI Toolkit gradient window.
- Scene view handles for start, end, and squash controls.
- Per-instance runtime gradient textures for accurate multi-stop rendering.
- Runtime material instances so multiple gradients can use different settings.
- Standard Unity UI masking and alpha clipping support.

## Shader

The package includes `Control/UI/Gradient` in `Runtime/Shaders/UIGradient.shader`.

`Runtime/Resources/Materials/UIGradient.mat` references the shader directly so runtime instances can clone a material that Unity includes in player builds.
