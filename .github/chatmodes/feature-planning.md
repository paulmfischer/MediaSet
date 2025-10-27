# GitHub Copilot Chat Mode: Feature Planning

This chat mode provides a structured approach for planning new features in the MediaSet project.

## Trigger

When a user asks to "plan out a feature" or requests a "feature plan", activate this mode.

## Process

### 1. Create Planning Document

Create a new markdown file in the `ImplementationDetails/` folder at the project root:
- **Naming Convention**: `<FEATURE_NAME>_IMPLEMENTATION.md`
- Use SCREAMING_SNAKE_CASE for the feature name
- Be descriptive but concise (e.g., `MOVIE_BARCODE_IMPLEMENTATION.md`, `ADVANCED_SEARCH_FILTERS_IMPLEMENTATION.md`)

### 2. Document Structure

The planning document should follow this template structure:

```markdown
# [Feature Name] Implementation Plan

## Overview

[2-3 paragraph summary of the feature]
- What is being built?
- Why is it needed?
- What value does it provide to users?

## Related Existing Functionality

[Document any existing code, components, or features that relate to this implementation]

### Backend Components
- List relevant services, models, APIs
- Note any reusable patterns or code

### Frontend Components
- List relevant components, routes, data fetchers
- Note any reusable UI patterns

### Infrastructure/Configuration
- Database schema considerations
- Environment variables or settings
- Third-party integrations

## Requirements

### Functional Requirements
1. [Requirement 1]
2. [Requirement 2]
3. [...]

### Non-Functional Requirements
- Performance expectations
- Accessibility standards
- Security considerations
- Scalability needs

## Proposed Changes

### Backend Changes (MediaSet.Api)

#### New/Modified Models
- **Model Name** (`path/to/Model.cs`)
  - Properties to add/modify
  - Validation rules
  - Database indexing needs

#### New/Modified Services
- **Service Name** (`path/to/Service.cs`)
  - Methods to implement
  - Dependencies needed
  - Business logic description

#### New/Modified API Endpoints
- **Endpoint**: `[HTTP_METHOD] /api/path`
  - Purpose
  - Request/Response models
  - Authorization requirements
  - Error handling

#### Database Changes
- Collections to add/modify
- Indexes to create
- Migration considerations

### Frontend Changes (MediaSet.Remix)

#### New/Modified Routes
- **Route**: `/path/to/route`
  - Purpose and user flow
  - Loader/action functions needed
  - Data dependencies

#### New/Modified Components
- **Component Name** (`path/to/Component.tsx`)
  - Props interface
  - State management needs
  - Child components
  - Styling approach

#### New/Modified Data Functions
- **Function Name** (`path/to/data.ts`)
  - API calls to make
  - Data transformations
  - Error handling

#### Type Definitions
- Interfaces to add to `models.ts`
- Type guards if needed

### Testing Changes

#### Backend Tests (MediaSet.Api.Tests)
- **Test Class**: `TestClassName`
  - Test scenarios to cover
  - Mock dependencies needed
  - Edge cases to validate

#### Frontend Tests (MediaSet.Remix)
- **Test File**: `TestFile.test.tsx`
  - Component interaction tests
  - User flow tests
  - Accessibility tests

#### Integration Tests
- End-to-end scenarios
- API contract validation

## Implementation Steps

1. [Step 1 - e.g., Create backend models]
2. [Step 2 - e.g., Implement service layer]
3. [Step 3 - e.g., Add API endpoints]
4. [Step 4 - e.g., Create frontend types]
5. [Step 5 - e.g., Implement UI components]
6. [Step 6 - e.g., Add tests]
7. [Step 7 - e.g., Documentation updates]

## Acceptance Criteria

- [ ] [Criterion 1 - e.g., Users can perform action X]
- [ ] [Criterion 2 - e.g., API returns correct data format]
- [ ] [Criterion 3 - e.g., UI displays loading states]
- [ ] [Criterion 4 - e.g., All tests pass]
- [ ] [Criterion 5 - e.g., Documentation updated]

## Risks and Mitigations

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| [Risk description] | High/Medium/Low | High/Medium/Low | [How to mitigate] |
| [Example: API rate limiting] | Medium | Medium | [Implement caching layer] |

## Open Questions

1. [Question 1 - e.g., Should we support feature X in this iteration?]
2. [Question 2 - e.g., What is the expected load/performance target?]
3. [Question 3 - e.g., Do we need backwards compatibility?]

## Dependencies

- External libraries/packages needed
- Third-party API integrations
- Infrastructure changes

## Timeline Estimate

- **Planning**: [X hours/days]
- **Backend Implementation**: [X hours/days]
- **Frontend Implementation**: [X hours/days]
- **Testing**: [X hours/days]
- **Documentation**: [X hours/days]
- **Total**: [X hours/days]

## References

- Related issues/tickets
- Design documents
- API documentation
- Similar implementations
```

### 3. Guidelines for Completing the Document

When creating a feature plan:

1. **Be Specific**: Include actual file paths, class names, and code references
2. **Be Thorough**: Consider all layers of the application (API, UI, DB, tests)
3. **Be Realistic**: Include risks and open questions rather than assuming everything will work perfectly
4. **Follow Code Style**: Reference the project's code style guidelines when proposing changes
5. **Consider Testing**: Always include testing strategy for new functionality
6. **Think About Edge Cases**: Document error handling, validation, and edge cases
7. **Reference Existing Code**: Look at similar features in the codebase for patterns to follow

### 4. After Creating the Plan

1. Review the plan with the user
2. Ask for clarification on open questions
3. Adjust based on feedback
4. Once approved, the plan serves as a roadmap for implementation
5. Commit the plan to a feature branch (following branch protection rules)

## Example Usage

**User**: "Plan out a movie barcode generation feature"

**Response**:
1. Create `ImplementationDetails/MOVIE_BARCODE_GENERATION_IMPLEMENTATION.md`
2. Fill in all sections with specific details about:
   - How movie barcodes work
   - Existing image handling in the codebase
   - Required backend endpoints for frame extraction
   - Frontend components for displaying barcodes
   - Testing approach for image generation
   - Acceptance criteria and risks

## Benefits

- **Structured Thinking**: Forces consideration of all aspects before coding
- **Documentation**: Serves as reference during and after implementation
- **Communication**: Makes it easy to discuss and review plans
- **Risk Management**: Identifies potential issues early
- **Scope Control**: Helps define clear boundaries for the feature

## Note

This planning mode is especially useful for:
- Complex features spanning multiple layers
- Features requiring external integrations
- Features with unclear requirements
- Features that modify core functionality
- Features that need careful testing strategy
