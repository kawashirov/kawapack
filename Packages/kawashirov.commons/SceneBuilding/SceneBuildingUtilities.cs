using System;

namespace Kawashirov.SceneBuilding {
	public enum BuildingStatus {
		NotStarted, Running, Cancelled, Failed, Success
	}

	public class CancelBuilding : Exception {
		public CancelBuilding() { }
		public CancelBuilding(string message) : base(message) { }
		public CancelBuilding(string message, Exception inner) : base(message, inner) { }
	}

	public class FailedToSaveBuildingScene : Exception {
		public FailedToSaveBuildingScene() { }
		public FailedToSaveBuildingScene(string message) : base(message) { }
		public FailedToSaveBuildingScene(string message, Exception inner) : base(message, inner) { }
	}
	
	public static class SceneBuildingUtilities {

	}
}