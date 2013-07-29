using UnityEngine;
using System.Collections;
using System.Collections.Generic;
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]

public class FindFaces : MonoBehaviour {
public GameObject meshHolder;
GameObject player;
bool lastHit;
bool meshDrawn;
int numberOfRays;
int vectorPointNumber = 0;
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
public Vector3[] normals;
public int[] triangles;
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
						hit.collider.gameObject.renderer.material.color = Color.red;						
						// get mesh of object hit by ray 
						Mesh meshHit = hit.collider.gameObject.GetComponent<MeshFilter>().mesh;	
						// determine size of vertices array for vertices of mesh
						vertices = new Vector3[meshHit.vertices.Length];
						// determine size of vertices array using only vertices that are facing down (i.e. the bottom of the polygon)
						
						// translate each vertice from local into world space
						vertices = meshHit.vertices;
						
						
						for (int i=0; i<meshHit.vertices.Length; i++)
							{
							// adjust for scale
							vertices[i] = new Vector3 (vertices[i].x*hit.collider.gameObject.transform.lossyScale.x, vertices[i].y*hit.collider.gameObject.transform.lossyScale.y,vertices[i].z*hit.collider.gameObject.transform.lossyScale.z);
							// adjust for world position
							vertices[i] = hit.collider.gameObject.transform.position - vertices[i];
							}
//						Debug.Log(vertices[0].x);
			
//						meshHit.GetTriangles
						// assign normals to array
						normals = meshHit.normals;
						triangles = meshHit.triangles;
						DrawMesh();			
			
//						Debug.Log(vertices[0]);
//						Debug.Log(vertices[1]);
//						Debug.Log ("hitpoint"+hit.point+"");
//						Debug.Log ("rayPos"+rayPos+"");
//						Debug.Log ("movePos"+movePos+"");
						Debug.DrawLine(transform.position, hit.point);
					}
				

		}
	
	void DrawMesh()
		
	{
	Mesh mesh = meshHolder.GetComponent<MeshFilter>().mesh;
	mesh.vertices = new Vector3[] {transform.position, vertices[0], vertices[1]};
	mesh.uv = new Vector2[] {transform.position, vertices[0], vertices[1]};
	mesh.triangles = new int[] {0, 1, 2};	
		
	}
}