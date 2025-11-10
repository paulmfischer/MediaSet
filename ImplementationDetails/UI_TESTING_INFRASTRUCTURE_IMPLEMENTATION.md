# UI Testing Infrastructure Implementation Plan

## Related Issue/Task

**GitHub Issue**: [#249](https://github.com/paulmfischer/MediaSet/issues/249)

*Add UI tests - We have workflows setup to run UI tests but no UI tests yet. Plan out an action to implement UI tests and to incrementally add UI tests.*

## Overview

The MediaSet frontend (Remix.js + React) currently has no UI tests despite having a GitHub Actions workflow configured to run them. This plan establishes a testing infrastructure with Vitest and React Testing Library, then incrementally adds UI tests across the codebase. The approach prioritizes:

1. **Test Infrastructure Setup** - Installing and configuring testing tools
2. **Utility Testing** - Testing helper functions and data utilities
3. **Component Testing** - Testing individual components systematically by type (forms, inputs, dialogs, cards, etc.)
4. **Route Testing** - Testing data loaders/actions and route-level functionality
5. **Workflow Integration** - Updating the CI/CD pipeline to run real tests

This phased approach allows for smaller PRs with focused test coverage, making it easier to review and maintain the test suite as it grows.

## Related Existing Functionality

### Frontend Structure
- **Routes**: `/app/routes/` contains Remix route handlers with data loaders and actions
  - Main routes: `_index.tsx`, `$entity/`, `$entity._index/`, `$entity_.$entityId/`, etc.
  - Lookup routes: `$entity_.lookup/`
  - CRUD routes: `$entity_.add/`, `$entity_.$entityId_.edit/`, `$entity_.$entityId_.delete/`
  
- **Components**: `/app/components/` contains reusable React components
  - Forms: `book-form.tsx`, `game-form.tsx`, `movie-form.tsx`, `music-form.tsx`
  - Inputs: `multiselect-input.tsx`, `singleselect-input.tsx`
  - UI Elements: `badge.tsx`, `spinner.tsx`, `footer.tsx`
  - Dialogs: `delete-dialog.tsx`
  - Navigation: `pending-navigation.tsx`
  - Cards: `StatCard.tsx`

- **Utilities**: 
  - `/app/helpers.ts` - Helper functions for data manipulation
  - `/app/models.ts` - TypeScript type definitions
  - `/app/entity-data.ts` - Entity-related data utilities
  - `/app/metadata-data.ts` - Metadata utilities
  - `/app/lookup-data.server.ts` - Server-side lookup data functions
  - `/app/stats-data.ts` - Statistics data utilities

### Existing Backend Testing
- Backend uses NUnit with Moq and Bogus for unit/integration tests
- Test structure mirrors source: `MediaSet.Api.Tests/` contains subdirectories matching `MediaSet.Api/`
- Tests cover: Services, Models, Converters, Clients, Entities, Helpers, Health checks, Metadata, Stats, Lookup strategies

### GitHub Actions Workflow
- PR Checks workflow (`.github/workflows/pr-checks.yml`) includes a placeholder `ui-test` job
- Job runs when UI files change: `MediaSet.Remix/**`
- Currently has placeholder step that echoes "UI tests are not yet implemented"
- Needs to be updated to run actual tests

## Requirements

### Functional Requirements
1. Set up Vitest and React Testing Library as testing framework
2. Configure test discovery and execution for `*.test.ts` and `*.test.tsx` files
3. Test all utility functions in `/app/helpers.ts`, `/app/models.ts`, etc.
4. Test form components (book, game, movie, music forms)
5. Test input components (multiselect, singleselect)
6. Test UI components (badge, spinner, delete-dialog, etc.)
7. Test route data functions (loaders/actions)
8. Update CI/CD workflow to run tests instead of placeholder
9. Achieve minimum code coverage thresholds (~70% overall: utilities 80%, components 65-75%, routes 70%)
10. Mock all API calls in tests to ensure isolated, fast test execution

### Non-Functional Requirements
- Tests should run in < 30 seconds in CI/CD
- Testing framework should integrate with Vite (existing build tool)
- Tests should be maintainable and follow project conventions
- TypeScript support for all test files
- Clear test output and coverage reports
- IDE support for running/debugging tests

## Proposed Changes

### Frontend Changes (MediaSet.Remix)

#### Configuration Files
- **`vitest.config.ts`** (new)
  - Configure Vitest with Vite integration
  - Set up jsdom environment for DOM testing
  - Configure coverage thresholds and reporting
  - Enable globals (describe, it, expect without imports)

- **`package.json`** (modify)
  - Add devDependencies: `vitest`, `@vitest/ui`, `@testing-library/react`, `@testing-library/user-event`, `jsdom`
  - Add test scripts: `npm run test`, `npm run test:watch`, `npm run test:coverage`

- **`tsconfig.json`** (modify)
  - Add Vitest type references for test globals
  - Include test files in compiler options if needed

#### Test Setup Files
- **`.github/workflows/pr-checks.yml`** (modify)
  - Replace placeholder UI test step with actual `npm run test` command
  - Add coverage reporting (optional)

- **`app/test-setup.ts`** (new, if needed)
  - Global test setup/teardown
  - Mock API calls or global utilities

#### Utility Tests

- **`app/helpers.test.ts`** (new)
  - Tests for all exported functions in `helpers.ts`
  - Coverage of data transformation, formatting, validation logic

- **`app/models.test.ts`** (new)
  - Tests for type guards/validation functions
  - Coverage of model structure validation

- **`app/entity-data.test.ts`** (new)
  - Tests for entity data fetching/processing functions

- **`app/metadata-data.test.ts`** (new)
  - Tests for metadata-related utilities

- **`app/stats-data.test.ts`** (new)
  - Tests for statistics calculation/processing

#### Component Tests (Phase 1 - Form Components)

- **`app/components/book-form.test.tsx`** (new)
  - Test form rendering
  - Test input changes and validation
  - Test form submission
  - Test initial value loading

- **`app/components/game-form.test.tsx`** (new)
  - Same structure as book-form tests

- **`app/components/movie-form.test.tsx`** (new)
  - Same structure as book-form tests

- **`app/components/music-form.test.tsx`** (new)
  - Same structure as book-form tests

#### Component Tests (Phase 2 - Input Components)

- **`app/components/singleselect-input.test.tsx`** (new)
  - Test dropdown rendering
  - Test option selection
  - Test search/filter functionality
  - Test keyboard navigation

- **`app/components/multiselect-input.test.tsx`** (new)
  - Test multiple selection
  - Test option removal
  - Test search functionality

#### Component Tests (Phase 3 - UI Components)

- **`app/components/delete-dialog.test.tsx`** (new)
  - Test dialog visibility
  - Test cancel/confirm actions
  - Test callback execution

- **`app/components/badge.test.tsx`** (new)
  - Test badge rendering with different props

- **`app/components/spinner.test.tsx`** (new)
  - Test spinner visibility

- **`app/components/StatCard.test.tsx`** (new)
  - Test card data display

- **`app/components/pending-navigation.test.tsx`** (new)
  - Test navigation state handling

#### Route Tests

- **`app/routes/_index.test.tsx`** (new)
  - Test home page rendering
  - Test data loader functionality

- **`app/routes/$entity._index/route.test.tsx`** (new)
  - Test entity list page rendering
  - Test data loader/action handling

- **`app/routes/$entity_.$entityId_.edit/route.test.tsx`** (new)
  - Test edit page rendering
  - Test form submission action

- **`app/routes/$entity_.add/route.test.tsx`** (new)
  - Test add page rendering
  - Test create action

- **`app/routes/$entity_.$entityId_.delete/route.test.tsx`** (new)
  - Test delete action
  - Test redirects

- **`app/routes/$entity_.lookup/route.test.tsx`** (new)
  - Test lookup form rendering
  - Test search action

### Mocking & Test Utilities

- **`app/test/mocks.ts`** (new)
  - Mock API responses for all endpoints
  - Mock fetch for server functions
  - Mock Remix loaders/actions context
  - Mock localStorage and other browser APIs as needed

- **`app/test/fixtures.ts`** (new)
  - Test data fixtures for entities (books, games, movies, music)
  - Realistic sample data for assertions
  - Mock API response payloads

- **`app/test/test-utils.tsx`** (new)
  - Custom render function with necessary providers (if any)
  - Common test helpers and utilities
  - Mocking setup for each test

## Implementation Steps

### Phase 0: Infrastructure Setup
1. Add testing dependencies to `package.json`
2. Create `vitest.config.ts` configuration
3. Update `tsconfig.json` with Vitest globals
4. Create test utilities and mocks (`test/mocks.ts`, `test/fixtures.ts`, `test/test-utils.tsx`)
5. Update `.github/workflows/pr-checks.yml` to run real tests
6. Create initial test run verification (no tests yet, should pass)

### Phase 1: Utility Testing
7. Write tests for `app/helpers.ts`
8. Write tests for `app/models.ts` and type validation
9. Write tests for `app/entity-data.ts`
10. Write tests for `app/metadata-data.ts`
11. Write tests for `app/stats-data.ts`
12. Verify >80% coverage on utilities

### Phase 2: Form Component Testing
13. Write tests for `book-form.tsx`
14. Write tests for `game-form.tsx`
15. Write tests for `movie-form.tsx`
16. Write tests for `music-form.tsx`
17. Verify >70% coverage on form components

### Phase 3: Input Component Testing
18. Write tests for `singleselect-input.tsx`
19. Write tests for `multiselect-input.tsx`
20. Verify >75% coverage on input components

### Phase 4: UI Component Testing
21. Write tests for `delete-dialog.tsx`
22. Write tests for `badge.tsx`
23. Write tests for `spinner.tsx`
24. Write tests for `StatCard.tsx`
25. Write tests for `pending-navigation.tsx`
26. Verify >65% coverage on UI components

### Phase 5: Route Testing
27. Write tests for main routes (home, entity list, add, edit, delete)
28. Write tests for lookup routes
29. Write tests for data loaders/actions
30. Verify >70% coverage on routes

### Phase 6: Documentation & Optimization
31. Document testing approach in `MediaSet.Remix/README.md`
32. Review and optimize test suite performance
33. Add test coverage badges to project README (using Codecov or similar)
34. Create testing guidelines for future contributions

## Testing Strategy

### Unit Tests (Utilities)
- Test pure functions with various inputs
- Mock external dependencies
- Cover edge cases and error conditions

### Component Tests
- Use React Testing Library for DOM testing
- Test user interactions (clicks, typing, selection)
- **Mock all API calls** using MSW (Mock Service Worker) or similar
- Test accessibility (ARIA attributes, keyboard navigation)
- Avoid testing implementation details

### Route Tests
- Mock Remix loader/action context
- **Mock API responses** to test data fetching and transformation
- Test data handling and error scenarios
- Test navigation and redirects
- Test error handling and edge cases

### Test Patterns
- Arrange-Act-Assert (AAA) structure
- Descriptive test names
- One assertion focus per test when possible
- Shared fixtures for common test data
- Custom `render` utility with necessary providers
- Mock all external API calls for isolation and speed

## Acceptance Criteria

- [ ] Testing framework (Vitest + React Testing Library) installed and configured
- [ ] `npm run test` runs all tests successfully (even if just setup tests)
- [ ] `npm run test:watch` enables watch mode for development
- [ ] `npm run test:coverage` generates coverage reports
- [ ] GitHub Actions workflow runs tests on PR
- [ ] All utility functions have >80% coverage
- [ ] All form components have >70% coverage  
- [ ] All input components have >75% coverage
- [ ] All UI components have >65% coverage
- [ ] All route handlers have >70% coverage
- [ ] Test documentation added to `MediaSet.Remix/README.md`
- [ ] All tests pass consistently in CI/CD
- [ ] Tests run in <30 seconds

## Sub-Tasks for Independent PRs

These sub-tasks are small enough to be tracked as separate GitHub issues and implemented in independent PRs:

### Infrastructure Phase (1 PR)
- [x] **Sub-task 1.1**: Setup Vitest, React Testing Library, and configuration
  - Add dependencies to package.json
  - Create vitest.config.ts
  - Update tsconfig.json
  - Create test utilities (test/test-utils.tsx, test/mocks.ts, test/fixtures.ts)
  - Update GitHub Actions workflow
  - **Estimated effort**: 2 hours

### Utility Testing Phase (5 PRs - one per utility file)
- [ ] **Sub-task 2.1**: Add helpers.ts unit tests
  - Test all exported functions
  - Target >85% coverage
  - **Estimated effort**: 1.5 hours

- [ ] **Sub-task 2.2**: Add models.ts type validation tests
  - Test type guards and validators
  - **Estimated effort**: 1 hour

- [ ] **Sub-task 2.3**: Add entity-data.ts tests
  - Test data fetching/processing
  - **Estimated effort**: 1 hour

- [ ] **Sub-task 2.4**: Add metadata-data.ts tests
  - Test metadata utilities
  - **Estimated effort**: 1 hour

- [ ] **Sub-task 2.5**: Add stats-data.ts tests
  - Test statistics functions
  - **Estimated effort**: 1 hour

### Form Component Testing Phase (4 PRs - one per form)
- [ ] **Sub-task 3.1**: Add book-form.tsx component tests
  - Test rendering, input changes, validation, submission
  - Target >70% coverage
  - **Estimated effort**: 2 hours

- [ ] **Sub-task 3.2**: Add game-form.tsx component tests
  - Same scope as book-form tests
  - **Estimated effort**: 2 hours

- [ ] **Sub-task 3.3**: Add movie-form.tsx component tests
  - Same scope as book-form tests
  - **Estimated effort**: 2 hours

- [ ] **Sub-task 3.4**: Add music-form.tsx component tests
  - Same scope as book-form tests
  - **Estimated effort**: 2 hours

### Input Component Testing Phase (2 PRs)
- [ ] **Sub-task 4.1**: Add singleselect-input.tsx component tests
  - Test dropdown, selection, keyboard navigation
  - Target >75% coverage
  - **Estimated effort**: 2 hours

- [ ] **Sub-task 4.2**: Add multiselect-input.tsx component tests
  - Test multiple selection, removal, search
  - Target >75% coverage
  - **Estimated effort**: 2 hours

### UI Component Testing Phase (5 PRs - one per component)
- [ ] **Sub-task 5.1**: Add delete-dialog.tsx component tests
  - Test visibility, actions, callbacks
  - **Estimated effort**: 1.5 hours

- [ ] **Sub-task 5.2**: Add badge.tsx component tests
  - Test rendering with props
  - **Estimated effort**: 1 hour

- [ ] **Sub-task 5.3**: Add spinner.tsx component tests
  - Test visibility and states
  - **Estimated effort**: 1 hour

- [ ] **Sub-task 5.4**: Add StatCard.tsx component tests
  - Test data display and formatting
  - **Estimated effort**: 1 hour

- [ ] **Sub-task 5.5**: Add pending-navigation.tsx component tests
  - Test navigation state handling
  - **Estimated effort**: 1 hour

### Route Testing Phase (5 PRs)
- [ ] **Sub-task 6.1**: Add home page (_index) route tests
  - Test rendering and data loader
  - **Estimated effort**: 1.5 hours

- [ ] **Sub-task 6.2**: Add entity list routes tests
  - Test listing, sorting, filtering
  - **Estimated effort**: 2 hours

- [ ] **Sub-task 6.3**: Add entity add/create route tests
  - Test form rendering and action handling
  - **Estimated effort**: 1.5 hours

- [ ] **Sub-task 6.4**: Add entity edit route tests
  - Test pre-population and update action
  - **Estimated effort**: 1.5 hours

- [ ] **Sub-task 6.5**: Add entity delete route tests and lookup routes
  - Test delete action and redirects
  - Test lookup form and search
  - **Estimated effort**: 2 hours

### Documentation & Finalization Phase (1 PR)
- [ ] **Sub-task 7.1**: Add testing documentation and coverage reporting
  - Update MediaSet.Remix/README.md with testing guide
  - Document patterns and conventions
  - Setup coverage badge/reports (Codecov integration)
  - Add instructions for running tests locally
  - **Estimated effort**: 1.5 hours

## Total Effort Estimation

| Phase | Sub-tasks | Estimated Effort |
|-------|-----------|------------------|
| Infrastructure | 1 | 2 hours |
| Utilities | 5 | 5 hours |
| Form Components | 4 | 8 hours |
| Input Components | 2 | 4 hours |
| UI Components | 5 | 5.5 hours |
| Route Testing | 5 | 8.5 hours |
| Documentation | 1 | 1.5 hours |
| **TOTAL** | **23** | **~34.5 hours** |

## Risks and Mitigations

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Complex form components difficult to test | Medium | Medium | Start with simpler components, use fixtures extensively |
| Async operations in loaders/actions | Medium | High | Use proper async/await patterns, mock timers when needed |
| Server-side data functions hard to mock | Medium | Medium | Create comprehensive mock utilities in test/mocks.ts |
| Tests become brittle (implementation-focused) | Medium | Medium | Focus on behavior, use Testing Library best practices |
| Test suite runs slowly in CI/CD | Low | Low | Use jsdom for speed, consider splitting into multiple jobs if needed |
| Remix-specific testing patterns unclear | Low | Medium | Reference Remix testing guide and create test-utils helpers |

## Decisions Made

1. **Coverage Targets**: Target ~70% overall coverage with phased approach (utilities 80%, components 65-75%, routes 70%)
2. **Integration Testing Strategy**: Mock all API calls in tests to ensure fast, reliable test execution without backend dependencies
3. **Visual Regression Testing**: Not needed for this phase; focus on functional testing first
4. **Test Coverage Badges**: Yes, setup test coverage badges and reports in Phase 6 (Documentation phase)
5. **E2E Testing**: Not planned for now; focus on unit/component/route testing first

## Dependencies

- **New Libraries**: Vitest, @testing-library/react, @testing-library/user-event, jsdom
- **Existing**: Vite (already configured), TypeScript, React
- **No external services needed** for unit/component tests
- Optional future: E2E testing framework (Playwright)

## References

- Remix Testing Guide: https://remix.run/docs/en/main/guides/testing
- React Testing Library: https://testing-library.com/react
- Vitest Documentation: https://vitest.dev/
- Testing Best Practices: https://kentcdodds.com/blog/common-mistakes-with-react-testing-library
