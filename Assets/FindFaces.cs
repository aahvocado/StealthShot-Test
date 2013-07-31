using UnityEngine;
using System.Collections;
using System.Collections.Generic;
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]

public class FindFaces : MonoBehaviour {
public GameObject meshHolder;
//GameObject player;
public GameObject[] polygon;
GameObject[] walls;	
int polyNumber;
//bool lastHit;
//bool meshDrawn;
public int numberOfRays;
//int vectorPointNumber = 0;
public int _verticesArraySize;
public int verticesArraySize;
//float angle;
//public List<Vector3> vectorList;
//public List<Vector3> vectorList2;
//Vector3 newVertices;	
//Vector3 prevVector;
//Vector3 _playerPos;
//Vector3 tmp;
//Vector3 tmp2;
//RaycastHit hit;
//Mesh mesh;
public Vector3[] vertices;
Vector3[] verticesTemp;
public Vector3[] normals;
public int[] triangles;
public List <Vector3> verticesList;
Vector3 verticesDirection;
//Vector3 verticesNow;
float verticesAngle;
public Vector3[] finalVertices;	

Vector3 movePos;
Vector3 rayPos;
	
	void Start () 
	{			
		//assign all shadow casting objects in scene to wall array
		walls = GameObject.FindGameObjectsWithTag("Walls");		
		
	}	

	void Update ()
		{				
			DetectObjects();
		}	
	
	// establishes vertices of all shadow casting objects in scene, check to see which vertices are visible by player
	public void DetectObjects()		
		{	
					// initiate array to include all polygons tagged 'wall'
					polygon = new GameObject[walls.Length];
					// list of all useful (facing down) vertices in scene		
					verticesList.Clear ();
					// number of polygons in scene
					polyNumber = 0;
					// number of useful vertices in scene
					verticesArraySize = 0;
					// cycle through walls, harvesting useful vertices
					foreach(GameObject wall in walls)		
						{						
						// highlight polygon being examined in red
						polygon[polyNumber] = wall.gameObject;
						polygon[polyNumber].renderer.material.color = Color.red;						
						// get mesh of current polygon
						Mesh meshHit = polygon[polyNumber].GetComponent<MeshFilter>().mesh;	
						// determine size of vertices array by defining how many vertices are facing down (i.e. the bottom of the polygon) using normals
						normals = meshHit.normals;
						_verticesArraySize = 0;
						for (int i=0; i<meshHit.normals.Length ; i++)
							{
							if (normals[i].z == 1)
								{
								_verticesArraySize++;
								}		
							}						
						// translate each vertice from local into world space						
						verticesTemp = meshHit.vertices;
						// assign size of vertices array, +1 to include player position
						vertices = new Vector3[_verticesArraySize+1];
						for (int i=1; i<_verticesArraySize+1; i++)
							{										
							// adjust for scale
							vertices[i] = new Vector3 (verticesTemp[i-1].x*polygon[polyNumber].transform.lossyScale.x, verticesTemp[i-1].y*polygon[polyNumber].transform.lossyScale.y,transform.position.z);
							// adjust for world position
							vertices[i] = polygon[polyNumber].transform.position - vertices[i];
							vertices[i].z = transform.position.z;							
//							// check to see if it's on the opposite side of the poly, assign the vertcies to player if it is
							if (Physics.Linecast (vertices[i], transform.position)) 
											{
											vertices[i] = transform.position;
											}
//							vertices[i] = new Vector3 ((transform.position.x + vertices[i].x) * 5, (transform.position.y + vertices[i].y) * 5, vertices[i].z);
							verticesList.Add (vertices[i]);	
							
							}
						polygon[polyNumber].renderer.material.color = Color.white;
						triangles = meshHit.triangles;
						polyNumber++;
						// add current poly vertex number to total number of vertices
						verticesArraySize = verticesArraySize + _verticesArraySize;
						
						}
					
						CheckVerticesAngle();		
						DrawMesh();	
		}
	
	//establish vertices angle in relation to player, sort in clockwise order
	public void CheckVerticesAngle()
	{	
	for (int i=0; i<verticesList.Count; i++)
		{
		// find angle
		float actualAngle = 0;
		verticesDirection = transform.position - verticesList[i];
		verticesAngle = Vector3.Angle(Vector3.left,verticesDirection);		
		if(AngleDir(Vector3.left,verticesDirection,Vector3.up)>0F)
				{
				actualAngle += verticesAngle;
				}
				else
				{
				actualAngle += 360F-verticesAngle;
				}
//		Debug.Log (i);
//		Debug.Log(verticesNow);
//		Debug.Log(actualAngle);	
		// sort vertices into ascending order

		}		
		
	}
	// variable to calculate 360 degree angles
	private float AngleDir(Vector3 fwd, Vector3 targetDir, Vector3 up) {
		Vector3 perp = Vector3.Cross(fwd, targetDir);
		float dir	 = Vector3.Dot(perp, up);
		if		(dir > 0F)	{ return  1F;}//RIGHT
		else if	(dir < 0F)	{ return -1F;}//LEFT
		else				{ return  0F;}
	}	
	
	
	void DrawMesh()
		
	{
	// create mesh for main lightmesh
	Mesh mesh = meshHolder.GetComponent<MeshFilter>().mesh;	
	finalVertices = new Vector3 [verticesList.Count+1];
	finalVertices[0] = transform.position;
	// assign triangles	
	triangles = new int [verticesArraySize*3];	
	for(int v = 1, t = 1; v < verticesArraySize; v++, t += 3)
		{			
			finalVertices[v] = verticesList[v-1];
			triangles[t] = v;
			triangles[t + 1] = v + 1;
		}
//		vertices[verticesArraySize-1] = new Vector3(0,0,0);
//		vertices[0] = new Vector3(transform.position.x,transform.position.y,0);
		finalVertices[verticesList.Count] = transform.position;
//		vertices[vectorList.Count] = meshHolder.transform.InverseTransformPoint(transform.position);
//		meshholder.transform.InverseTransformPoint(transform.position)
//		triangles[triangles.Length - 1] = 0;
		mesh.vertices = finalVertices;
//		mesh.uv = vertices;		
		mesh.triangles = triangles;
		verticesList.Clear ();
//		finalVertices = mesh.vertices;
		
	}
}