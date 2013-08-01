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
//public int numberOfRays;
//int vectorPointNumber = 0;
int _verticesArraySize;
int verticesArraySize;
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
public List <Vector3> verticesListTemp;
float verticesAngle;
public Vector3[] finalVertices;	
float[] actualAngle;
public float[] _actualAngle;
float tempval;

Vector3 movePos;
Vector3 rayPos;
	
	void Start () 
	{			
		//assign all shadow casting objects in scene to wall array
		walls = GameObject.FindGameObjectsWithTag("Walls");	
		DetectObjects();
		
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
							Vector3 rayPos = transform.position;
							rayPos.z = transform.position.z;
							if (Physics.Linecast (vertices[i], rayPos)) 
											{
											vertices[i] = transform.position;
											}				
//							RaycastHit hit;
//							Vector3 rayPos = transform.position-vertices[i];
//							rayPos.z = transform.position.z;
//							if (Physics.Raycast (vertices[i], rayPos, out hit))
//											{
//											vertices[i] = transform.position;
//											}
							Debug.DrawLine (vertices[i],rayPos);
//							vertices[i] = new Vector3 ((transform.position.x + vertices[i].x) * 5, (transform.position.y + vertices[i].y) * 5, vertices[i].z);
							verticesList.Add (vertices[i]);	
							
							}
//						polygon[polyNumber].renderer.material.color = Color.white;
						triangles = meshHit.triangles;
						polyNumber++;
						// add current poly vertex number to total number of vertices
						verticesArraySize = verticesArraySize + _verticesArraySize;
						
						}
					
						AssignVerticesAngles();		
//						DrawMesh();	
		}
	
	//establish vertices angle in relation to player, sort in clockwise order
	public void AssignVerticesAngles()
	{
	actualAngle = new float[verticesList.Count];	
	for (int i=0; i<verticesList.Count; i++)
		{
		// find angle of vertices
		verticesDirection = verticesList[i] - transform.position;
		verticesAngle = Vector3.Angle(verticesDirection,Vector3.left);		
		float dirNum = AngleDir(Vector3.left, verticesDirection, Vector3.forward); 		
			if(dirNum>0F)
				{
				actualAngle[i] += 360F-verticesAngle;				
				}
				else
				{
				actualAngle[i] += verticesAngle;
				}			
		}
	ArrangeVerticesCW();
//	DrawMesh();
	}
	
	//variable to turn acute 180 degree angles into 360 degree angles
	private float AngleDir(Vector3 fwd, Vector3 targetDir, Vector3 up) {
		Vector3 perp = Vector3.Cross(fwd, targetDir);
		float dir = Vector3.Dot(perp, up);		
		if (dir > 0f) {
			return 1f;
		} else if (dir < 0f) {
			return -1f;
		} else {
			return 0f;
		}
		
	}	
	
	//rearrange vertices into order moving clockwise around player
	public void ArrangeVerticesCW()
	{
	
	float sortCheckSize = actualAngle.Length;
//	Debug.Log (actualAngle.Length);
	int iV = 0;	
	while (sortCheckSize > 0)
		{		
//		Debug.Log ("sortCheck "+sortCheckSize+"");
		while (actualAngle[iV] == 361)
			{
			iV++;
			}
		if (actualAngle[iV] != 361)
			{
			tempval = actualAngle[iV];	
			}
//		Debug.Log("In ");
//		Debug.Log("iV = "+iV+"");
//		Debug.Log("In tempval "+tempval+"");
//		Debug.Log("In angle "+actualAngle[iV]+"");
		 for (int iN=0; iN<actualAngle.Length;iN++)
			{
//			Debug.Log("In 2 ");
//			Debug.Log("iN = "+iN+"");
//			Debug.Log("In 2 tempval "+tempval+"");
//			Debug.Log("In 2 angle "+actualAngle[iN]+"");						
			if ((tempval< actualAngle[iN]) || (tempval== actualAngle[iN]))
				{
					if (iN == actualAngle.Length-1)
					{
//					Debug.Log("Out 2 ");
//					Debug.Log("Out 2 "+tempval+"");
//					Debug.Log("Out 2 "+actualAngle[iV]+"");
					verticesListTemp.Add (verticesList[iV]);								
					sortCheckSize -= 1;
					actualAngle[iV] = 361;
					iV = 0;
					}
				}
			if (tempval> actualAngle[iN])
				{
//				Debug.Log("Out 1 ");
//				Debug.Log("Out 1 tempval "+tempval+"");
//				Debug.Log("Out 1 angle "+actualAngle[iN]+"");
				iV++;
//				Debug.Log(iV);
				iN = actualAngle.Length-1;				
				}
			}			
        }	
	DrawMesh();
	}
	
	
	
	// create mesh for main lightmesh
	void DrawMesh()		
	{	
	Mesh mesh = meshHolder.GetComponent<MeshFilter>().mesh;	
	finalVertices = new Vector3 [verticesListTemp.Count+1];
	finalVertices[0] = transform.position;
	// assign triangles	
	triangles = new int [verticesArraySize*3];	
	for(int v = 1, t = 1; v < verticesArraySize; v++, t += 3)
		{			
			finalVertices[v] = verticesListTemp[v-1];
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
		verticesListTemp.Clear();
//		finalVertices = mesh.vertices;
		
	}
}