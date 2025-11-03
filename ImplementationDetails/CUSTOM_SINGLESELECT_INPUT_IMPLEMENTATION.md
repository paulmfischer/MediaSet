# Custom Single-Select Input Implementation Plan

## Overview

This feature involves creating a new custom single-select input component (`singleselect-input.tsx`) that provides an enhanced dropdown experience similar to the existing `multiselect-input.tsx` component. Unlike the standard HTML `<select>` element, this custom component will allow users to:

1. Select a single value from a dropdown list of options
2. Manually add new items to the list that aren't in the predefined options
3. Display a selected value even if it doesn't appear in the list of selectable data
4. Navigate and interact with the component using keyboard controls for accessibility

This component will be particularly useful for fields like **Format**, **Publisher**, **Platform**, and **Label** across various entity forms (Books, Movies, Games, Music), where users might need to select from common values but occasionally need to add a custom value not in the list.

The component follows the same design patterns and accessibility standards as the existing `MultiselectInput` component, ensuring consistency in user experience across the application.

## Related Existing Functionality

### Frontend Components

#### MultiselectInput Component
- **File**: `MediaSet.Remix/app/components/multiselect-input.tsx`
- **Purpose**: Provides multi-value selection with ability to add custom values
- **Reusable Patterns**:
  - Combobox pattern with filter/search functionality
  - "Add new" option when typing custom text
  - Keyboard navigation (ArrowUp, ArrowDown, Enter, Escape)
  - Click-away overlay to close dropdown
  - Fixed positioning for dropdown menu with scroll handling
  - ARIA attributes for accessibility (role="combobox", role="listbox", etc.)
  - Badge display for selected items
  - Focus management with refs

#### Form Components Using Dropdowns
- **BookForm** (`MediaSet.Remix/app/components/book-form.tsx`)
  - Format field: Standard `<select>` dropdown
  - Publisher field: Standard `<select>` with workaround to display value not in list
- **MovieForm** (`MediaSet.Remix/app/components/movie-form.tsx`)
  - Format field: Standard `<select>` dropdown
- **GameForm** (`MediaSet.Remix/app/components/game-form.tsx`)
  - Format field: Standard `<select>` dropdown
  - Platform field: Standard text `<input>` (could benefit from suggestions)
- **MusicForm** (`MediaSet.Remix/app/components/music-form.tsx`)
  - Format field: Standard `<select>` dropdown
  - Label field: Standard `<select>` dropdown

#### Current Workaround Pattern
In `book-form.tsx`, there's a workaround for displaying publishers not in the list:
```tsx
{(() => {
  const allPublishers = [...publishers];
  if (book?.publisher && !publishers.find(p => p.value === book.publisher)) {
    allPublishers.push({ value: book.publisher, label: book.publisher });
  }
  return allPublishers.map(publisher => (
    <option key={publisher.value} value={publisher.value}>{publisher.label}</option>
  ));
})()}
```
This workaround should be eliminated once the new SingleSelectInput component is implemented.

### Type Definitions

- **File**: `MediaSet.Remix/app/models.ts`
- **Relevant Types**:
  - `Option` type (likely needs to be defined or shared between components)
  - `Metadata` type used in form props: `{ label: string; value: string; }`

### Styling Patterns

- **Tailwind CSS Classes**: Consistent with existing form inputs
  - Input styles: `border border-gray-600 bg-gray-800 text-white rounded-md`
  - Dropdown menu: `bg-gray-700 border border-gray-600 shadow-lg`
  - Hover/active states: `hover:bg-gray-600`, `bg-gray-600` for selected/active
  - Focus rings: `focus:outline-none focus:ring-2 focus:ring-blue-400`

## Requirements

### Functional Requirements

1. **Single Value Selection**: Component must allow selecting exactly one value from a dropdown list
2. **Custom Value Entry**: Users must be able to type and add custom values not in the predefined list
3. **Display Any Value**: Component must display the current value even if it's not in the options list
4. **Keyboard Navigation**: Full keyboard support including:
   - Arrow Up/Down to navigate options
   - Enter to select
   - Escape to close dropdown
   - Tab to move focus away
   - Typing to filter options
5. **Filter/Search**: Typing in the input should filter the list of available options
6. **Clear Selection**: Users should be able to clear the current selection using Backspace
7. **Add New Option**: When typing a value not in the list, display "Add new [value]" option
8. **Form Integration**: Component must work seamlessly with Remix form submissions

### Non-Functional Requirements

- **Accessibility**: WCAG 2.1 AA compliance with proper ARIA attributes
- **Performance**: Smooth filtering and rendering for lists up to ~100 options
- **Consistency**: Match the visual design and interaction patterns of existing components
- **Responsive**: Work correctly on mobile and desktop viewports
- **Browser Compatibility**: Support modern browsers (Chrome, Firefox, Safari, Edge)

## Proposed Changes

### Frontend Changes (MediaSet.Remix)

#### New Component

**Component**: `SingleselectInput` (`MediaSet.Remix/app/components/singleselect-input.tsx`)

**Props Interface**:
```tsx
type Option = {
  label: string;
  value: string;
  isNew?: boolean | undefined;
}

type SingleselectProps = {
  name: string;
  addLabel: string;      // e.g., "Add new Format:"
  placeholder: string;   // e.g., "Select Format..."
  options: Option[];
  selectedValue?: string;
}
```

**Key Features**:
- Display single selected value in input field
- Show dropdown menu with filtered options
- Support adding custom values with "Add new X" option
- Keyboard navigation (ArrowUp, ArrowDown, Enter, Escape, Backspace)
- Click-away to close dropdown
- Focus management
- ARIA attributes for screen readers

**State Management**:
- `selected: Option | null` - currently selected option
- `filterText: string` - text typed by user for filtering
- `displayOptions: boolean` - whether dropdown is open
- `activeIndex: number` - keyboard navigation index
- Refs for container, input, and option elements

**Component Structure**:
- Input field showing selected value or placeholder
- Click-away overlay
- Fixed-position dropdown menu with filtered options
- Hidden input for form submission
- "Add new" option when custom text is typed

#### Modified Components

**Component**: `BookForm` (`MediaSet.Remix/app/components/book-form.tsx`)

**Changes**:
- Import `SingleselectInput` component
- Replace Format `<select>` with `<SingleselectInput>`
- Replace Publisher `<select>` with `<SingleselectInput>` and remove workaround logic
- Props to pass: `name`, `addLabel`, `placeholder`, `options`, `selectedValue`

**Component**: `MovieForm` (`MediaSet.Remix/app/components/movie-form.tsx`)

**Changes**:
- Import `SingleselectInput` component
- Replace Format `<select>` with `<SingleselectInput>`

**Component**: `GameForm` (`MediaSet.Remix/app/components/game-form.tsx`)

**Changes**:
- Import `SingleselectInput` component
- Replace Format `<select>` with `<SingleselectInput>`
- Replace Platform text input with `<SingleselectInput>`

**Component**: `MusicForm` (`MediaSet.Remix/app/components/music-form.tsx`)

**Changes**:
- Import `SingleselectInput` component
- Replace Format `<select>` with `<SingleselectInput>`
- Replace Label `<select>` with `<SingleselectInput>`

#### Type Definitions

**File**: `MediaSet.Remix/app/models.ts`

**Changes**:
- Extract `Option` type to shared location for use by both multiselect and singleselect components:
  ```tsx
  export type Option = {
    label: string;
    value: string;
    isNew?: boolean | undefined;
  }
  ```
- Update `MultiselectInput` to import `Option` from `~/models`
- `SingleselectInput` will import `Option` from `~/models`

### Testing Changes

#### Frontend Tests (MediaSet.Remix)

**Test File**: `MediaSet.Remix/app/components/singleselect-input.test.tsx` (new)

**Test Scenarios**:
1. **Rendering**:
   - Renders with placeholder when no value selected
   - Renders with selected value when provided
   - Renders with value not in options list
2. **User Interactions**:
   - Clicking input opens dropdown
   - Clicking option selects it and closes dropdown
   - Clicking away closes dropdown
   - Typing filters the options list
   - Backspace clears selection when input is focused
3. **Custom Value Entry**:
   - Typing non-matching text shows "Add new" option
   - Selecting "Add new" option sets custom value
   - Custom value persists after selection
4. **Keyboard Navigation**:
   - ArrowDown highlights next option
   - ArrowUp highlights previous option
   - Enter selects highlighted option
   - Escape closes dropdown
   - Tab moves focus away
5. **Form Integration**:
   - Hidden input contains selected value
   - Form submission includes correct value
6. **Accessibility**:
   - Has correct ARIA attributes
   - Screen reader announces states correctly
   - Keyboard navigation works as expected

**Test File**: Form component tests (update existing or create new)
- Test that SingleselectInput integrates correctly in forms
- Test that form submission works with new component

## Implementation Steps

1. **Create SingleselectInput Component** (`singleselect-input.tsx`)
   - Set up component structure with TypeScript interfaces
   - Implement basic rendering with input and hidden form field
   - Add state management for selected value, filter text, dropdown visibility

2. **Implement Dropdown Menu**
   - Add fixed positioning dropdown with options list
   - Implement click-away overlay to close dropdown
   - Add filtered options logic based on user input
   - Implement "Add new" option for custom values

3. **Add Keyboard Navigation**
   - Implement ArrowUp/ArrowDown navigation with active index
   - Add Enter key to select option
   - Add Escape key to close dropdown
   - Add Backspace to clear selection when input is empty
   - Implement scroll-into-view for keyboard navigation

4. **Add Accessibility Features**
   - Add ARIA attributes (role="combobox", aria-expanded, aria-controls, etc.)
   - Add proper labeling and descriptions
   - Test with keyboard-only navigation
   - Test with screen reader

5. **Style the Component**
   - Apply Tailwind CSS classes consistent with existing design
   - Match hover/active/focus states from multiselect component
   - Ensure responsive design works on mobile

6. **Update BookForm**
   - Import SingleselectInput
   - Replace Format select with SingleselectInput
   - Replace Publisher select with SingleselectInput and remove workaround

7. **Update MovieForm**
   - Import SingleselectInput
   - Replace Format select with SingleselectInput

8. **Update GameForm**
   - Import SingleselectInput
   - Replace Format select with SingleselectInput
   - Consider replacing Platform input with SingleselectInput

9. **Update MusicForm**
   - Import SingleselectInput
   - Replace Format select with SingleselectInput
   - Replace Label select with SingleselectInput

10. **Add Component Tests**
    - Create test file for SingleselectInput
    - Write unit tests for all interactions
    - Write accessibility tests
    - Test integration with forms

11. **Manual Testing**
    - Test component in all forms (Book, Movie, Game, Music)
    - Test keyboard navigation thoroughly
    - Test with screen reader
    - Test on mobile devices
    - Test edge cases (empty list, single option, very long list, etc.)

12. **Documentation**
    - Add inline code comments for complex logic
    - Update README if component is reusable elsewhere
    - Document props and usage examples

## Acceptance Criteria

- [ ] SingleselectInput component renders correctly with provided props
- [ ] Component displays selected value or placeholder appropriately
- [ ] Component displays values not in the options list
- [ ] Existing entity data displays correctly in SingleselectInput when editing an entity
- [ ] Clicking input opens dropdown menu with filtered options
- [ ] Clicking away from component closes dropdown
- [ ] Typing in input filters the options list
- [ ] Typing custom text shows "Add new" option
- [ ] Selecting "Add new" option sets the custom value
- [ ] ArrowUp/ArrowDown keys navigate through options
- [ ] Enter key selects the highlighted option
- [ ] Escape key closes the dropdown
- [ ] Component works correctly in all form components (Book, Movie, Game, Music)
- [ ] Form submissions include the selected value correctly
- [ ] Component has proper ARIA attributes for accessibility
- [ ] Component works with keyboard-only navigation
- [ ] Component works correctly on mobile devices
- [ ] Workaround code in BookForm for Publisher is removed
- [ ] All existing form functionality continues to work
- [ ] Unit tests pass with good coverage
- [ ] Visual design matches existing components
- [ ] Performance is smooth with typical option list sizes

## Risks and Mitigations

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Breaking existing form functionality | High | Medium | Thorough testing of all forms before and after changes; implement component first, then gradually replace selects |
| Accessibility issues with custom dropdown | High | Medium | Follow WCAG guidelines strictly; test with screen readers; reference existing multiselect component patterns |
| Keyboard navigation conflicts with browser behavior | Medium | Low | Use preventDefault appropriately; test across browsers |
| Performance issues with large option lists | Medium | Low | Implement virtualization if needed; typical lists are small (<100 items) |
| Mobile usability issues | Medium | Medium | Test on actual mobile devices; ensure touch targets are adequate; consider mobile-specific interactions |
| Styling inconsistencies across forms | Low | Low | Use consistent Tailwind classes; reference existing input styles |
| Form serialization issues with custom values | Medium | Low | Use hidden input field for form submission; test form data thoroughly |

## Open Questions

1. **Empty option**: Should there be an explicit "None" or "Select..." option in the dropdown, or is an empty input sufficient?
   - **Recommendation**: Don't add empty option; empty input indicates no selection

2. **Autocomplete behavior**: Should the component autocomplete the input as user types (like browser autocomplete)?
   - **Recommendation**: No autocomplete; show filtered list only; user must select explicitly

3. **Option sorting**: Should options be sorted alphabetically, by frequency, or maintain original order?
   - **Recommendation**: Maintain original order (backend controls sorting); component is agnostic

4. **Case sensitivity**: Should filtering be case-sensitive?
   - **Recommendation**: Case-insensitive filtering (consistent with multiselect)

5. **Multiple instances**: Can there be multiple SingleselectInput components on the same form?
   - **Answer**: Yes, the name prop ensures each has unique IDs

## Dependencies

- **React**: Already in use (hooks: useState, useEffect, useMemo, useRef)
- **Remix**: Already in use (form integration)
- **TypeScript**: Already in use
- **Tailwind CSS**: Already in use for styling
- **lucide-react**: Already in use (for X icon on clear button)

No new external dependencies required.

## References

- Related Issue: [GitHub Issue #104](https://github.com/paulmfischer/MediaSet/issues/104)
- Existing Component: `MediaSet.Remix/app/components/multiselect-input.tsx`
- Form Components: 
  - `MediaSet.Remix/app/components/book-form.tsx`
  - `MediaSet.Remix/app/components/movie-form.tsx`
  - `MediaSet.Remix/app/components/game-form.tsx`
  - `MediaSet.Remix/app/components/music-form.tsx`
- ARIA Combobox Pattern: [WAI-ARIA Authoring Practices](https://www.w3.org/WAI/ARIA/apg/patterns/combobox/)
- Code Style Guide: `.github/code-style-ui.md`
