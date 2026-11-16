#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.App
{
    /// <summary>
    /// Lets a script driving a development build in a browser switch one cost at a time, to see where a phone's frame
    /// goes without a console: <c>unityGame.SendMessage('PerfBridge', 'Run', 'hide menu__rays')</c>. The answer is
    /// logged as <c>[perf-bridge] …</c>. Not in a release build.
    /// </summary>
    /// <remarks>
    /// Commands: <c>hide</c>/<c>show</c> &lt;class or name&gt; (every element of the UI with it, by display),
    /// <c>nobg</c> and <c>noimg</c> &lt;class or name&gt; (clears their background colour or image),
    /// and whatever <see cref="PerfDevTools.Run"/> takes (<c>scale</c>, <c>msaa</c>, <c>outline</c>, <c>fog</c>,
    /// <c>world</c>, <c>mech</c>).
    /// </remarks>
    public sealed class PerfBridge : MonoBehaviour
    {
        private Func<string, string> run;
        private Func<VisualElement> ui;

        public void Bind(Func<string, string> run, Func<VisualElement> ui)
        {
            this.run = run;
            this.ui = ui;
        }

        // Called by name from the page.
        public void Run(string command)
        {
            string answer;
            try {
                answer = Execute(command.Trim());
            } catch (Exception exception) {
                answer = "failed: " + exception.Message;
            }

            Debug.Log($"[perf-bridge] {command}: {answer}");
        }

        private string Execute(string command)
        {
            var space = command.IndexOf(' ');
            var verb = space < 0 ? command : command.Substring(0, space);
            var argument = space < 0 ? "" : command.Substring(space + 1).Trim();
            if (verb is not ("hide" or "show" or "nobg" or "noimg")) {
                return run?.Invoke(command) ?? "not bound";
            }

            var root = ui?.Invoke();
            if (root == null) {
                return "no UI";
            }

            var count = 0;
            root.Query<VisualElement>().ForEach(element => {
                if (element.name != argument && !element.ClassListContains(argument)) {
                    return;
                }

                switch (verb) {
                    case "nobg":
                        element.style.backgroundColor = Color.clear;
                        break;
                    case "noimg":
                        element.style.backgroundImage = StyleKeyword.None;
                        break;
                    default:
                        element.style.display = verb == "hide" ? DisplayStyle.None : StyleKeyword.Null;
                        break;
                }

                count++;
            });
            return $"{count} elements";
        }
    }
}
#endif
