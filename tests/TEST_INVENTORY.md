# PFD Test Inventory

> **All tests in this project are UI/E2E tests** using Playwright for browser automation.
> They test the actual user interface through simulated user interactions.

## Prerequisites

Before running tests, ensure the Blazor server is running:
```bash
cd src/PFD.Blazor
dotnet run
```

The server should be accessible at `https://localhost:7010`

---

## Quick Commands

### Run All Tests
```bash
cd tests/PFD.PlaywrightTests/PFD.PlaywrightTests
dotnet test
```

### Run Tests by Category
```bash
# Planner UI Tests
dotnet test --filter "FullyQualifiedName~PlannerTests"

# Task State Transition Tests
dotnet test --filter "FullyQualifiedName~TaskStateTransitionTests"
```

### Run Single Test
```bash
dotnet test --filter "Name=Planner_AddTask_ShouldNotFreeze"
```

### Run with Verbose Output
```bash
dotnet test --logger "console;verbosity=detailed"
```

---

## Test Categories

### 1. PlannerTests (28 unique tests, 43 total with parameters)

| Test Name | Parameters | Description |
|-----------|------------|-------------|
| `DayRoller_ScrollAndClick_ShouldChangeDate` | 1-10 | Verifies day roller date selection works |
| `DayRoller_DragScroll_ShouldNotFreeze` | - | Tests drag scrolling doesn't freeze UI |
| `Theme_Switch_ShouldApplyAndHold` | 14 themes | Tests each theme applies correctly |
| `Theme_SwitchAllThemes_Sequentially` | - | Cycles through all themes in sequence |
| `Planner_AddTask_ShouldNotFreeze` | - | Tests task creation doesn't freeze |
| `Planner_ToggleViews_ShouldNotFreeze` | - | Tests Daily/Weekly toggle |
| `Calendar_NavigateMonths_ShouldNotFreeze` | - | Tests calendar navigation |
| `DragAndDrop_TaskToSchedule_ShouldScheduleTask` | - | Tests drag-drop scheduling |
| `DragAndDrop_MultipleDrops_ShouldNotFreeze` | - | Tests multiple drag operations |
| `DragHandle_ShouldBeVisible_ForUnscheduledTasks` | - | Verifies drag handles appear |
| `PanelResize_DragLeft_ShouldExpandRightPanel` | - | Tests panel resize left |
| `PanelResize_DragRight_ShouldShrinkRightPanel` | - | Tests panel resize right |
| `DragAndDrop_TasksTabToRightPanelTimeSlot_ShouldScheduleTask` | - | Tests cross-panel drag |
| `DragAndDrop_WaitingTabToRightPanel_ShouldMoveAndScheduleTask` | - | Tests waiting task scheduling |
| `TasksAndWaiting_ShouldPersistWhenDateChanges` | - | Tests tab persistence |
| `AddTask_ShouldAppearInTasksTab_Immediately` | - | Tests immediate task visibility |
| `Search_ShouldFindTask_ThatAppearsInTasksTab` | - | Tests search functionality |
| `MobileDayRoller_ClickDate_ShouldSyncHeaderAndSchedule` | - | Mobile: date click sync |
| `MobileDayRoller_ScrollAndStop_ShouldSyncToCenteredDate` | - | Mobile: scroll sync |
| `MobileDayRoller_SelectedDateAndHeader_ShouldAlwaysMatch` | - | Mobile: header consistency |
| `MobileDayRoller_CenterFrame_ShouldHighlightSelectedDate` | - | Mobile: selection highlight |

**Run Command:**
```bash
dotnet test --filter "FullyQualifiedName~PlannerTests"
```

---

### 2. TaskStateTransitionTests (54 total tests)

#### Waiting → Tasks (10 tests)
| Test Name | Description |
|-----------|-------------|
| `WaitingToTasks_MoveToToday_ShouldTransition(1-10)` | Tests "Move to today" button |

**Run Command:**
```bash
dotnet test --filter "Name~WaitingToTasks"
```

---

#### Tasks → Scheduled via Time Editor (10 tests)
| Test Name | Description |
|-----------|-------------|
| `TasksToScheduled_AddTime_ShouldTransition(1-10)` | Tests scheduling via time editor |

**Run Command:**
```bash
dotnet test --filter "Name~TasksToScheduled_AddTime"
```

---

#### Tasks → Scheduled via Drag-Drop (10 tests)
| Test Name | Description |
|-----------|-------------|
| `TasksToScheduled_DragDrop_ShouldTransition(1-10)` | Tests drag-drop to time slots |

**Run Command:**
```bash
dotnet test --filter "Name~TasksToScheduled_DragDrop"
```

---

#### Scheduled → Waiting (10 tests)
| Test Name | Description |
|-----------|-------------|
| `ScheduledToWaiting_PastDateTask_ShouldAppearInWaiting(1-10)` | Tests past tasks appear in Waiting |

**Run Command:**
```bash
dotnet test --filter "Name~ScheduledToWaiting"
```

---

#### Scheduled → Tasks (10 tests)
| Test Name | Description |
|-----------|-------------|
| `ScheduledToTasks_ClearTime_ShouldTransition(1-10)` | Tests clearing scheduled time |

**Run Command:**
```bash
dotnet test --filter "Name~ScheduledToTasks"
```

---

#### Batch Tests (3 tests)
| Test Name | Description |
|-----------|-------------|
| `BatchTransition_10TasksFromWaitingToTasks` | Moves 10 waiting tasks at once |
| `BatchTransition_10TasksFromTasksToScheduled` | Schedules 10 tasks at once |
| `BatchTransition_10TasksFromScheduledToTasks` | Clears time on 10 tasks |

**Run Command:**
```bash
dotnet test --filter "Name~BatchTransition"
```

---

#### Full Cycle Test (1 test)
| Test Name | Description |
|-----------|-------------|
| `FullCycle_TasksToScheduledToWaitingToTasks_ShouldComplete` | Complete state machine cycle |

**Run Command:**
```bash
dotnet test --filter "Name=FullCycle_TasksToScheduledToWaitingToTasks_ShouldComplete"
```

---

## UI Tests by Feature

All tests are end-to-end UI tests using Playwright. Here's how they're organized by UI feature:

### Core UI Interactions
| Feature | Tests | Command |
|---------|-------|---------|
| Task Creation | `Planner_AddTask_ShouldNotFreeze`, `AddTask_ShouldAppearInTasksTab_Immediately` | `dotnet test --filter "Name~AddTask"` |
| Task Completion | `ToggleTask` (in state tests) | `dotnet test --filter "Name~Toggle"` |
| Task Deletion | Covered in state transition tests | - |
| Search | `Search_ShouldFindTask_ThatAppearsInTasksTab` | `dotnet test --filter "Name~Search"` |

### Navigation & Date Selection
| Feature | Tests | Command |
|---------|-------|---------|
| Day Roller | `DayRoller_ScrollAndClick_ShouldChangeDate` (x10), `DayRoller_DragScroll_ShouldNotFreeze` | `dotnet test --filter "Name~DayRoller"` |
| Calendar | `Calendar_NavigateMonths_ShouldNotFreeze` | `dotnet test --filter "Name~Calendar"` |
| View Toggle | `Planner_ToggleViews_ShouldNotFreeze` | `dotnet test --filter "Name~ToggleViews"` |

### Drag & Drop Operations
| Feature | Tests | Command |
|---------|-------|---------|
| Task to Schedule | `DragAndDrop_TaskToSchedule_ShouldScheduleTask` | `dotnet test --filter "Name~DragAndDrop"` |
| Cross-Panel Drag | `DragAndDrop_TasksTabToRightPanelTimeSlot_ShouldScheduleTask` | `dotnet test --filter "Name~DragAndDrop"` |
| Waiting to Schedule | `DragAndDrop_WaitingTabToRightPanel_ShouldMoveAndScheduleTask` | `dotnet test --filter "Name~DragAndDrop"` |
| Multiple Drops | `DragAndDrop_MultipleDrops_ShouldNotFreeze` | `dotnet test --filter "Name~DragAndDrop"` |
| Drag Handles | `DragHandle_ShouldBeVisible_ForUnscheduledTasks` | `dotnet test --filter "Name~DragHandle"` |

### Panel & Layout
| Feature | Tests | Command |
|---------|-------|---------|
| Panel Resize Left | `PanelResize_DragLeft_ShouldExpandRightPanel` | `dotnet test --filter "Name~PanelResize"` |
| Panel Resize Right | `PanelResize_DragRight_ShouldShrinkRightPanel` | `dotnet test --filter "Name~PanelResize"` |
| Tab Persistence | `TasksAndWaiting_ShouldPersistWhenDateChanges` | `dotnet test --filter "Name~Persist"` |

### Theming
| Feature | Tests | Command |
|---------|-------|---------|
| Individual Themes | `Theme_Switch_ShouldApplyAndHold` (x14 themes) | `dotnet test --filter "Name~Theme"` |
| Theme Cycling | `Theme_SwitchAllThemes_Sequentially` | `dotnet test --filter "Name~Theme"` |

### Mobile UI
| Feature | Tests | Command |
|---------|-------|---------|
| Date Click Sync | `MobileDayRoller_ClickDate_ShouldSyncHeaderAndSchedule` | `dotnet test --filter "Name~Mobile"` |
| Scroll Sync | `MobileDayRoller_ScrollAndStop_ShouldSyncToCenteredDate` | `dotnet test --filter "Name~Mobile"` |
| Header Match | `MobileDayRoller_SelectedDateAndHeader_ShouldAlwaysMatch` | `dotnet test --filter "Name~Mobile"` |
| Selection Frame | `MobileDayRoller_CenterFrame_ShouldHighlightSelectedDate` | `dotnet test --filter "Name~Mobile"` |

### Task State Transitions (UI Workflow Tests)
| Workflow | Tests | Command |
|----------|-------|---------|
| Waiting → Tasks | 10 tests via "Move to Today" button | `dotnet test --filter "Name~WaitingToTasks"` |
| Tasks → Scheduled (Time Editor) | 10 tests via time picker modal | `dotnet test --filter "Name~TasksToScheduled_AddTime"` |
| Tasks → Scheduled (Drag) | 10 tests via drag-drop | `dotnet test --filter "Name~TasksToScheduled_DragDrop"` |
| Scheduled → Waiting | 10 tests via past date | `dotnet test --filter "Name~ScheduledToWaiting"` |
| Scheduled → Tasks | 10 tests via clear time | `dotnet test --filter "Name~ScheduledToTasks"` |
| Batch Operations | 3 tests (10 tasks each) | `dotnet test --filter "Name~BatchTransition"` |
| Full Cycle | 1 complete workflow test | `dotnet test --filter "Name=FullCycle"` |

---

## Quick Test Suites

### Smoke Test (Quick validation)
```bash
dotnet test --filter "Name=Planner_AddTask_ShouldNotFreeze|Name=DayRoller_ScrollAndClick_ShouldChangeDate"
```

### UI Responsiveness Tests
```bash
dotnet test --filter "Name~ShouldNotFreeze"
```

### Theme Tests
```bash
dotnet test --filter "Name~Theme"
```

### Drag-and-Drop Tests
```bash
dotnet test --filter "Name~DragAndDrop|Name~DragDrop"
```

### Mobile Tests
```bash
dotnet test --filter "Name~Mobile"
```

### Task State Transition Tests (All)
```bash
dotnet test --filter "FullyQualifiedName~TaskStateTransitionTests"
```

---

## Test Statistics

| Category | Unique Tests | With Parameters | Total Executions |
|----------|--------------|-----------------|------------------|
| PlannerTests | 21 | 24 | 43 |
| TaskStateTransitionTests | 7 | 50 | 54 |
| **Total** | **28** | **74** | **97** |

---

## Periodic Test Schedule Recommendations

### Daily (CI/CD)
- Smoke tests only (~2 min)
```bash
dotnet test --filter "Name=Planner_AddTask_ShouldNotFreeze|Name=AddTask_ShouldAppearInTasksTab_Immediately"
```

### After Each Feature
- Related category tests (~5-10 min)

### Weekly
- Full test suite (~15-20 min)
```bash
dotnet test
```

### Before Release
- Full suite with retry on failure
```bash
dotnet test --logger "trx" -- NUnit.NumberOfTestWorkers=1
```

---

## Troubleshooting

### Server Not Running
```
Microsoft.Playwright.PlaywrightException: net::ERR_CONNECTION_REFUSED
```
**Solution:** Start the Blazor server first

### Test User Not Created
Tests use `playwright_test` / `test1234` - the test suite auto-registers if needed

### Timeout Errors
Increase timeout or run with single worker:
```bash
dotnet test -- NUnit.NumberOfTestWorkers=1
```

### Screenshot on Failure
Add to test setup for debugging:
```csharp
await Page.ScreenshotAsync(new() { Path = "failure.png" });
```
