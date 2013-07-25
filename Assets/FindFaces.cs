using UnityEngine;
using System.Collections;
using System.Collections.Generic;
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]

public class FindFaces : MonoBehaviour {
GameObject meshHolder;
GameObject player;
bool lastHit;
bool meshDrawn;
int numberOfRays;
int vectorPointNumber = 0;
float angle;
public List<Vector3> vectorList;
public List<Vector3> vectorList2;
Vector3 newVertices;	
Vector3 prevVector;
Vector3 _playerPos;
Vector3 tmp;
Vector3 tmp2;
RaycastHit hit;
Mesh mesh;
public Vector3[] vertices;
public Vector3 movePos;
public Vector3 rayPos;
	
	void Start () 
	{			
//		vertices = new Vector3[numberOfRays];
//		mesh = meshHolder.GetComponent<MeshFilter>().mesh;
//		DrawMesh();	
	}	

	void Update ()
	{	
//		if (meshDrawn == true)
//			{
			DetectObjects();
//			}
		}	
	
	void DetectObjects()		
		{	
//		angle = 0;
			Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);							
			movePos = new Vector3(mousePos.x, mousePos.y, transform.position.z);
			rayPos  = new Vector3(mousePos.x, mousePos.y, 0);
				
				if (Physics.Raycast(transform.position, rayPos, out hit))
			      	{		
						Mesh meshHit = hit.collider.gameObject.GetComponent<MeshFilter>().mesh;	
						Debug.Log(meshHit.vertices.Length);
						vertices = new Vector3[meshHit.vertices.Length];						
						for (int i=0; i<meshHit.vertices.Length; i++)
						{
						vertices[i] = transform.TransformPoint(meshHit.vertices[i]);
						}
						Debug.Log(vertices[0]);
						Debug.Log(vertices[1]);
						Debug.DrawLine(transform.position, movePos);						
						Debug.DrawLine(transform.position, hit.point);
					}	

	}
}