# Shift-Key Alignment Constraint Feature

## Overview

Added support for toggling alignment constraint on/off during clone drag operations in the clipboard image editor. This allows users to quickly snap cloned annotations to horizontal or vertical alignment with the source position.

## User Workflow

When cloning annotations/controls in the clipboard image editor (Ctrl+Click+Drag):

1. **Ctrl+Drag** = Clone with freeform positioning (existing behavior)
2. **Ctrl+Shift+Drag** = Clone with alignment constraint (snap to horizontal OR vertical alignment)
3. **Release Shift (keep Ctrl)** = Revert to freeform; position moves freely with mouse
4. **Press Shift again (Ctrl still held)** = Re-snap to nearest alignment (horizontal or vertical to source)

## Alignment Logic

When Shift is held during clone drag, the dragged element snaps to one of two axes:

- **Horizontal alignment**: Y-position matches source, X follows mouse freely
- **Vertical alignment**: X-position matches source, Y follows mouse freely

The feature automatically detects which axis provides better alignment (smaller distance to source) and snaps to that one:

- If `|currentX - sourceX| <= |currentY - sourceY|`, snap horizontally (keep Y = source.Y)
- Otherwise, snap vertically (keep X = source.X)

## Implementation Details

### Helper Method

**`ConstrainPositionToAxis(Point sourcePos, Point currentPos) -> Point`**

Calculates the constrained position by snapping to the axis that's closer to the source:

```csharp
private static Point ConstrainPositionToAxis(Point sourcePos, Point currentPos)
{
    var dx = Math.Abs(currentPos.X - sourcePos.X);
    var dy = Math.Abs(currentPos.Y - sourcePos.Y);
    
    if (dx <= dy)
        return new Point(currentPos.X, sourcePos.Y);  // Horizontal alignment
    else
        return new Point(sourcePos.X, currentPos.Y);  // Vertical alignment
}
```

### State Tracking

For each annotation type, added state variables to track clone drag:

- `_textCloneDragInProgress` + `_textCloneDragOriginalCenter`
- `_mlCloneDragInProgress` + `_mlCloneDragOriginalCenter` (measure line)
- `_arrowCloneDragInProgress` + `_arrowCloneDragOriginalCenter`
- `_xCloneDragInProgress` + `_xCloneDragOriginalCenter`
- `_rectCloneDragInProgress` + `_rectCloneDragOriginalCenter`

### Drag Handler Modifications

For each annotation type, modified three event handlers:

1. **MouseLeftButtonDown** (clone creation)
   - Sets `_<type>CloneDragInProgress = true`
   - Stores `_<type>CloneDragOriginalCenter` (center of original annotation)

2. **MouseMove** (drag positioning)
   - Checks if Shift is held and clone drag is in progress
   - Calculates constrained position using `ConstrainPositionToAxis()`
   - Applies constrained position instead of unconstrained drag delta

3. **MouseLeftButtonUp** (drag completion)
   - Clears `_<type>CloneDragInProgress = false`

### Affected Annotation Types

The feature works for all clone-able annotations:

1. **Text Labels**
   - Canvas-level drag (hitbox around text)
   - Text display element drag
   
2. **Measure Lines**
   - Body drag (translates both start and end points)

3. **Arrows**
   - Body drag (uses OffsetX/OffsetY positioning)

4. **X Annotations**
   - Body drag (translates entire X shape)

5. **Rectangles**
   - Body drag on border
   - Body drag on hitZone

## Technical Notes

### Position Calculation Approaches

Different annotation types use different positioning models:

- **Absolute positioning**: Text, X, Rect (Bounds with X, Y, W, H)
- **Relative positioning**: Arrow (OffsetX, OffsetY from target center)
- **Point-based positioning**: Measure line (StartPt, EndPt)

The constraint logic is adapted for each model:

- **Text/X/Rect**: Constrain position directly
- **Arrow**: Calculate current center, constrain, then convert back to offset
- **Measure line**: Constrain start point, apply same delta to end point

### Existing Behavior Preserved

- All existing drag behavior is fully preserved
- Constraint only applies when both Shift is held AND clone drag is in progress
- Regular (non-cloned) drags are unaffected
- Other Shift-key functionality (e.g., Shift-snap for arrows during creation) is unchanged

## Testing Checklist

- [x] Build succeeds with no compilation errors
- [x] All existing tests pass (2224 tests)
- [x] Ctrl+Drag cloning works normally (no Shift constraint)
- [ ] Ctrl+Shift+Drag activates constraint (manual test)
- [ ] Release Shift mid-drag reverts to freeform (manual test)
- [ ] Press Shift mid-drag re-applies constraint (manual test)
- [ ] All annotation types support the feature (manual test)
- [ ] Undo/redo work correctly with constrained drags (manual test)

## Future Enhancements

Potential improvements for future iterations:

1. **Visual Feedback**: Show a faint guideline when constraint is active
2. **Lock Icon**: Display a small lock icon when in constrained mode
3. **Sensitivity**: Add configurable snap distance threshold
4. **Grid Snapping**: Combine with existing grid snap functionality
5. **Angle Display**: For arrows, show current angle when constrained
