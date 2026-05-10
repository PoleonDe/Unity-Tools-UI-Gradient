# Control Tools - UI Gradient

`Control.Gradient` is a `MaskableGraphic` for drawing configurable gradients in Unity UI.

## Features

- Linear, radial, angular, and diamond gradient modes.
- Scene view handles for start, end, and squash controls.
- Runtime material instances so multiple gradients can use different settings.
- Standard Unity UI masking and alpha clipping support.

## Shader

The package includes `Control/UI/Gradient` in `Runtime/Shaders/UIGradient.shader`.

`Runtime/Resources/Materials/UIGradient.mat` references the shader directly so runtime instances can clone a material that Unity includes in player builds.
