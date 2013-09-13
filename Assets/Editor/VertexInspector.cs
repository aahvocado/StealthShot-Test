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
		if (findFaces.meshBuilt == true)
		{
			for(int i = 0; i< findFaces.finalVertices.Length; i++)
			{
			Handles.Label (findFaces.finalVertices[i], ""+i+"");
			Handles.color = Color.blue;
			Handles.Label (new Vector3(100,0,83), "Vertices");
			}
		}
		if (findFaces.polyScanned == true)
		{	
			for(int i = 0; i< findFaces.polyNumber; i++)
			{
			Handles.Label (findFaces.polygon[i].transform.position, ""+i+"");
			Handles.color = Color.red;
			Handles.Label (new Vector3(100,20,83), "Polys");
			}
		}
		
	}
}
