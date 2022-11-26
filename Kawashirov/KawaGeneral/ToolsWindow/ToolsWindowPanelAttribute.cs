using System;

namespace Kawashirov.ToolsGUI {

	[AttributeUsage(AttributeTargets.Class)]
	public class ToolsWindowPanelAttribute : Attribute {

		public string path { get; private set; }

		public ToolsWindowPanelAttribute(string path) {
			this.path = path;
		}
	}
}

