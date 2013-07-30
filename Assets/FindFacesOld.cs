using UnityEngine;
using System.Collections;
using System.Collections.Generic;
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]

public class FindFacesOld : MonoBehaviour {
public GameObject meshHolder;
GameObject player;
GameObject polygon;
bool lastHit;
bool meshDrawn;
int numberOfRays;
int vectorPointNumber = 0;
public int verticesArraySize;
float angle;
//public List<Vector3> vectorList;
//public List<Vector3> vectorList2;
Vector3 newVertices;	
Vector3 prevVector;
Vector3 _playerPos;
Vector3 tmp;
Vector3 tmp2;
RaycastHit hit;
//Mesh mesh;
public Vector3[] vertices;
Vector3[] verticesTemp;
public Vector3[] normals;
public int[] triangles;
public Vector3[] finalVertices;	

Vector3 movePos;
Vector3 rayPos;
	
	void Start () 
	{			
//		vertices = new Vector3[numberOfRays];
//		mesh = meshHolder.GetComponent<MeshFilter>().mesh;
//		DrawMesh();	
	}	

	void Update ()
		{	
			DetectObjects();

		}	
	
	void DetectObjects()		
		{	
			Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);							
			movePos = new Vector3(mousePos.x, mousePos.y, transform.position.z);
			// 2nd raycast variable is a direction, so mousepos needs to be changed to a direction for raycast
			rayPos = movePos - transform.position;		
				if (Physics.Raycast(transform.position, rayPos, out hit))
			      	{		
						
						// highlight object being hit
						polygon = hit.collider.gameObject;
						polygon.renderer.material.color = Color.red;						
						// get mesh of object hit by ray 
						Mesh meshHit = polygon.GetComponent<MeshFilter>().mesh;	
						// determine size of vertices array using only vertices that are facing down (i.e. the bottom of the polygon) using normals
						normals = meshHit.normals;
						verticesArraySize = 0;
						for (int i=0; i<meshHit.normals.Length ; i++)
							{
							if (normals[i].z == 1)
								{
								verticesArraySize++;
								}		
							}						
						// translate each vertice from local into world space						
						verticesTemp = meshHit.vertices;
						vertices = new Vector3[verticesArraySize+1];
						for (int i=1; i<verticesArraySize+1; i++)
							{										
							// adjust for scale
							vertices[i] = new Vector3 (verticesTemp[i-1].x*polygon.transform.lossyScale.x, verticesTemp[i-1].y*polygon.transform.lossyScale.y,transform.position.z);
							// adjust for world position
							vertices[i] = polygon.transform.position - vertices[i];
							vertices[i].z = transform.position.z;
							// check to see if it's on the opposite side of the poly, assign the vertcies to player if it is
							if (Physics.Linecast (vertices[i], transform.position)) 
											{
											vertices[i] = transform.position;
											}
							}							
						triangles = meshHit.triangles;						
						DrawMesh();			
						
//						Debug.Log(vertices[0]);
//						Debug.Log(vertices[1]);
//						Debug.Log ("hitpoint"+hit.point+"");
//						Debug.Log ("rayPos"+rayPos+"");
//						Debug.Log ("movePos"+movePos+"");
//						Debug.DrawLine(transform.position, hit.point);
					}
				

		}
	
	void DrawMesh()
		
	{
	// create mesh for meshholder
	Mesh mesh = meshHolder.GetComponent<MeshFilter>().mesh;
	// assign triangles	
	triangles = new int [verticesArraySize*3];
	for(int v = 1, t = 1; v < verticesArraySize; v++, t += 3)
		{			
			triangles[t] = v;
			triangles[t + 1] = v + 1;
		}
//		vertices[verticesArraySize-1] = new Vector3(0,0,0);
//		vertices[0] = new Vector3(transform.position.x,transform.position.y,0);
		vertices[0] = transform.position;
//		vertices[vectorList.Count] = meshHolder.transform.InverseTransformPoint(transform.position);
//		meshholder.transform.InverseTransformPoint(transform.position)
//		triangles[triangles.Length - 1] = 0;
		mesh.vertices = vertices;
//		mesh.uv = vertices;		
		mesh.triangles = triangles;
		finalVertices = mesh.vertices;
		
	}
}