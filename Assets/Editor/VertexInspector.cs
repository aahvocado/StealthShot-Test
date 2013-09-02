using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;

[CustomEditor(typeof(FindFaces))]
public class VertexInspector : Editor {
public FindFaces findFaces;
GUISkin handles;	
	
	void OnEnable () {	
	}	
	
	
	public void OnSceneGUI () {
		handles = Resources.Load("Handles")as GUISkin;
		GUI.skin = handles;
		GameObject TestSphere = GameObject.Find("Test Sphere");
		FindFaces findFaces = (FindFaces) TestSphere.GetComponent(typeof(FindFaces));
		if (findFaces.meshBuilt == true){
			for(int i = 0; i< findFaces.finalVertices.Length; i++)
			{
			Handles.Label (findFaces.finalVertices[i], ""+i+"");
			Handles.color = Color.blue;
			Handles.Label (new Vector3(100,0,83), "Vertices");
			
//			Debug.Log(findFaces.finalVertices[0]);
			}
		}
	}
}
