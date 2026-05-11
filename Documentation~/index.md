# Control Tools - UI Gradient

`Control.Gradient` is a `MaskableGraphic` for drawing configurable gradients in Unity UI.

## Features

- Linear, radial, angular, and diamond gradient modes.
- Multi-stop color and alpha editing through a custom UI Toolkit gradient window.
- Scene view handles for start, end, and squash controls.
- Per-instance runtime gradient textures for accurate multi-stop rendering.
- Runtime material instances so multiple gradients can use different settings.
- Standard Unity UI masking and alpha clipping support.
- Gradient-driven soft masks for alpha-fading child graphics.

## Soft Masks

Use `Control/UI Soft Mask` on a gradient object and `Control/UI Soft Maskable` on referenced graphics when child alpha should follow the gradient alpha.

The soft mask component generates a runtime alpha texture from the source gradient. Maskable children sample that texture in mask-local coordinates and multiply their output alpha.

`UI Soft Maskable` uses its explicit `Soft Mask` reference, so the masked graphic does not need to be parented under the mask. `Show Mask Graphic` controls whether the source gradient draws visibly. The generated mask texture resolution is automatic and based on the mask rect size and canvas scale.

Unity's built-in `Mask` remains useful for hard stencil clipping, but it does not provide grayscale alpha masking.

## Shader

The package includes `Control/UI/Gradient` in `Runtime/Shaders/UIGradient.shader`.

`Runtime/Resources/Materials/UIGradient.mat` and `Runtime/Resources/Materials/UISoftMask.mat` reference package shaders directly so runtime instances can clone materials that Unity includes in player builds.
