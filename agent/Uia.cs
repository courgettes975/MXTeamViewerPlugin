namespace MXTeamViewer.Agent
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Windows.Automation;

    // Microsoft UI Automation helpers. Elements are matched by AutomationId first (language
    // independent, e.g. "meeting", "search-input-field" in TeamViewer 15.83), then by any of the
    // configured names (FR + EN by default) - names are configurable in config.json.
    internal static class Uia
    {
        public static AutomationElement Find(IntPtr hwnd, string automationId, string[] names)
        {
            if (hwnd == IntPtr.Zero || !Native.IsWindow(hwnd))
            {
                return null;
            }
            var root = AutomationElement.FromHandle(hwnd);
            if (!string.IsNullOrEmpty(automationId))
            {
                var byId = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));
                if (byId != null)
                {
                    return byId;
                }
            }
            foreach (var name in names ?? Array.Empty<string>())
            {
                var byName = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name, PropertyConditionFlags.IgnoreCase));
                if (byName != null)
                {
                    return byName;
                }
            }
            return null;
        }

        public static bool Invoke(IntPtr hwnd, string automationId, string[] names, out string how)
        {
            var element = Find(hwnd, automationId, names);
            if (element == null)
            {
                how = "element not found";
                return false;
            }
            if (element.TryGetCurrentPattern(InvokePattern.Pattern, out var p))
            {
                ((InvokePattern)p).Invoke();
                how = "InvokePattern";
                return true;
            }
            if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out p))
            {
                ((SelectionItemPattern)p).Select();
                how = "SelectionItemPattern";
                return true;
            }
            if (element.TryGetCurrentPattern(TogglePattern.Pattern, out p))
            {
                ((TogglePattern)p).Toggle();
                how = "TogglePattern";
                return true;
            }
            if (element.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out p))
            {
                ((ExpandCollapsePattern)p).Expand();
                how = "ExpandCollapsePattern";
                return true;
            }
            how = "no actionable pattern";
            return false;
        }

        public static bool Focus(IntPtr hwnd, string automationId, string[] names)
        {
            var element = Find(hwnd, automationId, names);
            if (element == null)
            {
                return false;
            }
            // A group (search field container) is not focusable: focus its first edit/focusable child.
            var target = element.Current.IsKeyboardFocusable
                ? element
                : element.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.IsKeyboardFocusableProperty, true));
            if (target == null)
            {
                return Invoke(hwnd, automationId, names, out _);
            }
            target.SetFocus();
            return true;
        }

        public static string Dump(IntPtr hwnd, int max)
        {
            var sb = new StringBuilder();
            if (!Native.IsWindow(hwnd))
            {
                return "invalid window";
            }
            var root = AutomationElement.FromHandle(hwnd);
            sb.AppendLine($"WINDOW 0x{hwnd.ToInt64():X} [{root.Current.Name}] class={root.Current.ClassName}");
            var all = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
            sb.AppendLine($"  {all.Count} descendants");
            var n = 0;
            foreach (AutomationElement e in all)
            {
                var c = e.Current;
                if (string.IsNullOrEmpty(c.Name) && string.IsNullOrEmpty(c.AutomationId))
                {
                    continue;
                }
                var patterns = string.Join(",", e.GetSupportedPatterns().Select(x => x.ProgrammaticName.Replace("PatternIdentifiers.Pattern", "")));
                sb.AppendLine($"  {c.ControlType.ProgrammaticName.Replace("ControlType.", "")} | {c.Name} | id={c.AutomationId} | {patterns}");
                if (++n >= max)
                {
                    sb.AppendLine("  ...");
                    break;
                }
            }
            return sb.ToString();
        }
    }
}
