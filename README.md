# Source Engine Movement in Unity

A minimalist extension to Unity's Character Controller that enables Source and Quake style movement, such as bhopping and surfing.

This character controller only works properly with the new input system.

# Setup

## Create an instance of MoveVars

1. Under Assets/Resources, right click > Create > Scriptable Objects > MoveVars.
2. Name ScriptableObject "MoveVars" for now.
3. Now you can tweak and test out different values for the movement variables in the editor using this MoveVars asset.

## Input System Setup

1. Make sure the new Input System Package is installed.
2. Under Edit > Project Settings > Input System Package, create actions ```Jump``` and ```Crouch```. Space is recommended for Jump and Control is recommended for Crouch.