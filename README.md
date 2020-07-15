# kawapack
My shitty things for Unity (Generally for VRChat).

Includes:

* **Commons** - things used in my other scripts
  * **Refreshables** - concept of editor-time helper things (ScriptableObjects assets and/or Components in scenes) to automete some editing processes that can be "refreshed".
  * **ShaderBaking** - base scripts for generating shaders. This is to not use a infinities of keywords and to fix some issues with material properties in VRC.
* **FlatLitToon** - My modular flat lit toon shader with various features.
* **LightProbeGenerator** - Script for placing lightprobes
* **Retro Sprites** - My edit of Retro Sprites shader
* **UndertaleDeath** - Tesselation FX shader, makes death effect like in Undertale. Made for ZpyduCat
* **UdonScripts** - My Udon scripts.
  * **AnimatorPropSave** - Saves Animators parameters on disable and loads on enable. So parameters not get reset.
  * **Doors** - My physical doors with sync.
  * **LazyPlayerPresenceTrigger** - simple player presence trigger
  * **SlowUpdate** - Limits calls of Update() per frame for a specific Udon scripts
  * **SmartStations** - My stations scripts.
* **UdonEditorCommons** - things used in my Udon scripts, basically refreshables.
