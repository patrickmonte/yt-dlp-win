## 2024-09-05 - Enhance WPF Accessibility with Labels
**Learning:** Replaced `<TextBlock>` labels with `<Label>` in WPF `MainWindow.xaml` to enable keyboard accelerators (e.g. `_URL`) and binding to target inputs using `Target="{Binding ElementName=Control}"`. Keeping `Padding="0"` maintains identical visual layout to `TextBlock`. Added `AutomationProperties.Name` to standalone inputs for screen readers.
**Action:** Always prefer `<Label>` over `<TextBlock>` when placing pseudo-labels next to inputs for screen readers and keyboard navigation.
