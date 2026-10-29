# UI review checklist

The sweep checks the layout and the focus mechanically. See [Sweep](SKILL.md#sweep). A person must check these items. Look at the screenshots of the recipe and of `sweep.md`, and go through each item for each page that the change touches.

## Copy

- Each label, button, and message says what happens, in the words of the user. No internal names, no codes, no placeholders.
- A message for an error says what went wrong and what the user can do.
- The same thing has the same name on each page.
- An empty state says why the list is empty and gives the next action.

## Content order

- The most important content comes first, also at 320 px, where the columns stack.
- A heading comes before the content that it names.
- The reading order and the Tab order are the same as the visual order.

## Focus target

- After an action removes, hides, or disables the focused element, the focus goes to a target that makes sense: the next row, the heading, or the trigger. The sweep finds only that the focus went to the body.
- When a dialog closes, the focus goes back to its trigger, or to the element that the action made.
- The focus ring shows on each element that the keyboard can reach.

## Look

- Each state shows correctly in the light and the dark theme: hover, focus, pressed, disabled, loading, error, and empty.
- The controls in one row have the same height. The spacing is the same between items of the same kind.
