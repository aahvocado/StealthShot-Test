using UnityEngine;
using System.Collections;
using System.Collections.Generic;
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]

public class RaycastShadows : MonoBehaviour {
public GameObject meshHolder;
public GameObject player;
public bool lastHit;
public bool meshDrawn;
public int numberOfRays;
int vectorPointNumber = 0;
public float angle;
public List<Vector3> vectorList;
public List<Vector3> vectorList2;
Vector3 newVertices;	
public Vector3 prevVector;
public Vector3 _playerPos;
Vector3 tmp;
Vector3 tmp2;
RaycastHit hit;
Mesh mesh;
Vector3[] vertices;
	
	void Start () 
	{			
		vertices = new Vector3[numberOfRays];
		mesh = meshHolder.GetComponent<MeshFilter>().mesh;
		DrawMesh();	
	}	

	void Update ()
	{	
		if (meshDrawn == true)
			{
			DetectObjects();
			}
		}	
	
	void DetectObjects()		
		{	
		vertices = mesh.vertices;
//		vectorList.Clear();
//		vectorList.Add (meshHolder.transform.InverseTransformPoint(transform.position));
//		Debug.Log (vectorList[0]);
		vertices[0] = meshHolder.transform.InverseTransformPoint(transform.position);
		angle = 0;
		for (int i=1; i<numberOfRays; i++)
			{
			angle += 2*Mathf.PI/numberOfRays;
			Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);			
				if (Physics.Raycast(transform.position, direction, out hit))
			      	{						
						tmp = meshHolder.transform.InverseTransformPoint(hit.point);
						vertices[i] = (new Vector3(tmp.x,tmp.y,0));
//						Debug.Log (""+i+ ""+vertices[i]+"");
						Debug.Log(hit.triangleIndex);
						lastHit = true;

					}
				else
					{	
//						if (lastHit == true)
//							{
//							Debug.Log (""+i+ "");
							tmp2 = meshHolder.transform.InverseTransformPoint(transform.position+direction);
							vertices[i] = (new Vector3(tmp2.x,tmp2.y,0));
//							Debug.Log (""+i+ ""+vertices[i]+"");
							lastHit = false;
//							}
					}
			
			}
		vertices[numberOfRays] = meshHolder.transform.InverseTransformPoint(transform.position);
//		Debug.Log (""+numberOfRays+ ""+vertices[numberOfRays]+"");
		mesh.vertices = vertices;
	}	
	
	
	void DrawMesh()
	{
		
		vectorList.Clear();
		vectorList.Add (meshHolder.transform.InverseTransformPoint(transform.position));
//		Debug.Log (vectorList[0]);
		angle = 0;
		for (int i=0; i<numberOfRays; i++)
			{
			angle += 2*Mathf.PI/numberOfRays;
			Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);	
			
				if (Physics.Raycast(transform.position, direction, out hit)&& lastHit ==false)
			      	{						
						tmp = meshHolder.transform.InverseTransformPoint(hit.point);
						vectorList.Add (new Vector3(tmp.x,tmp.y,0));
						lastHit = true;
					}
				else
					{	
//						if (lastHit == true)
//							{
							tmp2 = meshHolder.transform.InverseTransformPoint(transform.position+direction);
							vectorList.Add (new Vector3(tmp2.x,tmp2.y,0));
							lastHit = false;
//							}
					}
			
			
			}		
		
		
//		Mesh mesh = meshHolder.GetComponent<MeshFilter>().mesh;
		mesh.Clear();		
		vertices = mesh.vertices;
		Vector2[] uv = mesh.uv;
		vertices = new Vector3 [vectorList.Count+1];
		uv = new Vector2 [vectorList.Count+1];
		int[] triangles = mesh.triangles;
		triangles = new int [vectorList.Count*3];
//		vertices[0] = vectorList[0];
		for(int v = 1, t = 1; v < vectorList.Count; v++, t += 3)
		{
			vertices[v] = vectorList[v];
			triangles[t] = v;
			triangles[t + 1] = v + 1;
		}
		vertices[vectorList.Count-1] = new Vector3(0,0,0);
//		vertices[0] = new Vector3(player.transform.position.x,player.transform.position.y,0);
//		vertices[vectorList.Count] = meshHolder.transform.InverseTransformPoint(transform.position);
//		meshholder.transform.InverseTransformPoint(transform.position)
		triangles[triangles.Length - 1] = 0;
		mesh.vertices = vertices;
//		for (int i=0; i<vectorList.Count; i++)
//			{
//			Debug.Log (""+i+ ""+vertices[i]+"");			
//			}
//			Debug.Log (""+vectorList.Count+ ""+vertices[vectorList.Count]+"");
		mesh.uv = uv;		
		mesh.triangles = triangles;
		meshDrawn = true;
		lastHit = false;
	}
}
