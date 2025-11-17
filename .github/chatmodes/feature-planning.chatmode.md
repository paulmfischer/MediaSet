---
description: 'Create comprehensive implementation plans for new features with detailed technical specifications and step-by-step breakdown. Includes autonomous execution of git commands and implementation steps with minimal user intervention.'
tools: ['codebase', 'search', 'searchResults', 'usages', 'fetch', 'githubRepo', 'problems', 'new', 'edit', 'runInTerminal']
---

# Feature Planning

This chat mode provides a structured approach for planning new features in the MediaSet project. When activated, it creates detailed implementation plans in the `ImplementationDetails/` folder and can autonomously execute the implementation with git operations.

## Autonomous Execution Mode

When using this mode, the assistant will:
1. **Automatically create feature branches** using git commands
2. **Execute implementation steps** from the plan autonomously
3. **Make periodic commits** with appropriate commit messages
4. **Check progress** and report status at each major step
5. **Handle errors** and ask for guidance only when needed

The user only needs to:
- Provide the feature request or point to an implementation plan
- Review progress updates at key milestones
- Approve the final pull request

## Planning Workflow

### 1. Create Planning Document

Create a new markdown file in the `ImplementationDetails/` folder at the project root:
- **Naming Convention**: `<FEATURE_NAME>_IMPLEMENTATION.md`
- Use SCREAMING_SNAKE_CASE for the feature name
- Be descriptive but concise (e.g., `MOVIE_BARCODE_IMPLEMENTATION.md`, `ADVANCED_SEARCH_FILTERS_IMPLEMENTATION.md`)

### 2. Document Structure

The planning document should follow this template structure:

```
# [Feature Name] Implementation Plan

## Related Issue/Task

**GitHub Issue**: [#ISSUE_NUMBER](https://github.com/paulmfischer/MediaSet/issues/ISSUE_NUMBER)

*Replace ISSUE_NUMBER with the actual issue number. If no issue exists yet, create one first or note "To be created".*

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
Make these sub-tasks that get added to the parent issue using https://github.com/yahsan2/gh-sub-issue extension.

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

## References

- Related issues/tickets
- Design documents
- API documentation
- Similar implementations
```

### 3. Guidelines for Completing the Document

When creating a feature plan:

1. **Link to GitHub Issue**: Always include a link to the GitHub issue/task that describes this feature request
2. **Be Specific**: Include actual file paths, class names, and code references
3. **Be Thorough**: Consider all layers of the application (API, UI, DB, tests)
4. **Be Realistic**: Include risks and open questions rather than assuming everything will work perfectly
5. **Follow Code Style**: Reference the project's code style guidelines when proposing changes
6. **Consider Testing**: Always include testing strategy for new functionality
7. **Think About Edge Cases**: Document error handling, validation, and edge cases
8. **Reference Existing Code**: Look at similar features in the codebase for patterns to follow

### 4. After Creating the Plan

1. Review the plan with the user
2. Ask for clarification on open questions
3. Adjust based on feedback
4. Once approved, the plan serves as a roadmap for implementation
5. Commit the plan to a feature branch (following branch protection rules)

### 5. Implementation and Commits

When implementing the feature plan:

1. **Check Current Branch**: First check which branch you're on
   ```bash
   git branch --show-current
   ```

2. **Create Feature Branch**: If not already on a feature branch, create one
   ```bash
   git checkout -b feature/[feature-name]
   ```
   - Use descriptive names: `feature/music-barcode-lookup`, `feature/advanced-filters`, etc.
   - Branch from `main` or the current working branch as appropriate

3. **Commit the Plan First**: Commit the implementation plan document as the first commit
   ```bash
   git add ImplementationDetails/FEATURE_NAME_IMPLEMENTATION.md
   git commit -m "docs: add implementation plan for [feature name] [AI-assisted] refs #ISSUE_NUMBER"
   ```
   - Replace `#ISSUE_NUMBER` with the actual GitHub issue number
   - Use `refs #N` to reference the issue without closing it

4. **Autonomous Implementation**: Execute implementation steps from the plan
   - The assistant will work through steps systematically
   - Create files, modify code, run tests as specified
   - Report progress at each major milestone
   - Handle compilation errors and fix issues automatically where possible

5. **Commit Periodically**: Make commits as you complete related changes or implementation steps
   - Commit after completing each major step in the implementation plan
   - Group related changes together (e.g., all model changes, all service changes)
   - Each commit should represent a logical unit of work
   
6. **Follow Commit Message Conventions**:
   - Use conventional commit format: `type(scope): description`
   - Types: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`
   - Always include `[AI-assisted]` tag or co-author trailer
   - Always reference the GitHub issue with `refs #N` for partial work or `closes #N` for completion
   - Examples:
     ```bash
     git commit -m "feat(api): add barcode lookup models [AI-assisted] refs #228"
     git commit -m "feat(api): implement barcode lookup service [AI-assisted] refs #228"
     git commit -m "feat(api): add barcode lookup endpoints [AI-assisted] refs #228"
     git commit -m "feat(ui): add barcode lookup components [AI-assisted] refs #228"
     git commit -m "test(api): add barcode lookup service tests [AI-assisted] closes #228"
     ```

7. **Commit Frequency Guidelines**:
   - After completing backend models and database changes
   - After implementing each service or significant business logic
   - After adding API endpoints
   - After implementing frontend types/interfaces
   - After creating/modifying UI components
   - After adding tests for a component/service
   - After updating documentation
   - When switching between major tasks (backend → frontend)

8. **Keep Commits Focused**: Each commit should be:
   - Self-contained and buildable (when possible)
   - Easy to review and understand
   - Reversible if needed
   - Descriptive of what changed and why

9. **Progress Checkpoints**: The assistant will pause and report progress:
   - After completing backend implementation
   - After completing frontend implementation
   - After adding tests
   - Before final commit
   - User can continue or provide feedback at these checkpoints

10. **Push and PR**: Once implementation is complete
    ```bash
    git push origin feature/[feature-name]
    ```
    - Assistant will provide summary of changes
    - User opens PR manually to review and merge

## Example Usage

### Planning Only

**User**: "Plan out a movie barcode generation feature"

**Response**:
1. Ask user for the GitHub issue number
2. Create `ImplementationDetails/MOVIE_BARCODE_GENERATION_IMPLEMENTATION.md`
3. Fill in all sections with specific details about:
   - Link to the related GitHub issue
   - How movie barcodes work
   - Existing image handling in the codebase
   - Required backend endpoints for frame extraction
   - Frontend components for displaying barcodes
   - Testing approach for image generation
   - Acceptance criteria and risks

### Planning + Autonomous Execution

**User**: "Implement the feature outlined in #file:MUSIC_BARCODE_LOOKUP_IMPLEMENTATION.md"

**Response**:
1. Check current branch with `git branch --show-current`
2. Create feature branch: `git checkout -b feature/music-barcode-lookup`
3. Begin implementing steps from the plan (all commits reference the issue):
   - Step 1: Create backend models → commit with `refs #123`
   - Step 2: Implement MusicBrainz client → commit with `refs #123`
   - Step 3: Implement MusicLookupStrategy → commit with `refs #123`
   - *[Progress update: Backend complete, moving to frontend]*
   - Step 4: Update TypeScript models → commit with `refs #123`
   - Step 5: Update lookup data function → commit with `refs #123`
   - Step 6: Update MusicForm component → commit with `closes #123`
   - *[Progress update: Frontend complete, implementation finished]*
4. Push branch: `git push origin feature/music-barcode-lookup`
5. Provide summary and next steps for PR (which will close the issue when merged)

## Benefits

- **Structured Thinking**: Forces consideration of all aspects before coding
- **Documentation**: Serves as reference during and after implementation
- **Communication**: Makes it easy to discuss and review plans
- **Risk Management**: Identifies potential issues early
- **Scope Control**: Helps define clear boundaries for the feature
- **Autonomous Execution**: Reduces manual work with automatic git operations and implementation
- **Consistency**: Follows project conventions for commits, branches, and code style
- **Progress Tracking**: Regular checkpoints keep user informed without requiring constant input

## When to Use

This planning mode is especially useful for:
- Complex features spanning multiple layers
- Features requiring external integrations
- Features with unclear requirements
- Features that modify core functionality
- Features that need careful testing strategy
