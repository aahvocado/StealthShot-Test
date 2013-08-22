using UnityEngine;
using System.Collections;
using System.Collections.Generic;
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]

public class FindFaces : MonoBehaviour {
public GameObject meshHolder;
public GameObject marker;
public GameObject[] polygon;
//public Vector3[] polygonPos;
public float Shadowlength;
GameObject[] walls;	
GameObject[] _marker;
int polyNumber;
//bool lastHit;
//bool meshDrawn;
//public int numberOfRays;
//int vectorPointNumber = 0;
int _verticesArraySize;
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
public List <Vector3> polyTransform;
public List <GameObject> polygonNumber;
public List <Vector3> verticesList;
public List <Vector3> _verticesList;
public List <Vector3> verticesAngles;
public List <Vector3> verticesByNormals;
Vector3 verticesDirection;
public List <Vector3> verticesListTemp;
float verticesAngle;
public Vector3[] finalVertices;	
float[] actualAngle;
public float[] _actualAngle;
float tempval;
bool getPolys;

Vector3 movePos;
Vector3 rayPos;
	
	void Start () 
	{			
		//assign all shadow casting objects in scene to wall array
		walls = GameObject.FindGameObjectsWithTag("Walls");	

		ScanForObjects();
		DetectObjectVertices();	
		
	}	

	void Update ()
		{
		//assign all shadow casting objects in scene to wall array
		walls = GameObject.FindGameObjectsWithTag("Walls");
		ScanForObjects();
		DetectObjectVertices();	
		}
	
	// finds objects in scene, arranges into CW order from 9'Oclock
	public void ScanForObjects()	
	{
		// initiate array to include all polygons tagged 'wall'					
		polygon = new GameObject[walls.Length];
//					polygonPos = new Vector3[walls.Length];
		polyNumber = 0;
		polyTransform.Clear ();
		foreach(GameObject wall in walls)
			{
			polygon[polyNumber] = wall.gameObject;
			// find lowest angle vertices
			Mesh polyMesh = polygon[polyNumber].GetComponent<MeshFilter>().mesh;
			normals = polyMesh.normals;
			verticesTemp = polyMesh.vertices;						
			for (int i=0; i<polyMesh.normals.Length ; i++)
				{
				if (normals[i].z == 1)
					{								
					// adjust vertices for scale of poly
					verticesTemp[i] = new Vector3 (verticesTemp[i].x*polygon[polyNumber].transform.lossyScale.x, verticesTemp[i].y*polygon[polyNumber].transform.lossyScale.y,transform.position.z);
					// adjust for world position
					verticesTemp[i] = polygon[polyNumber].transform.position - verticesTemp[i];
					verticesTemp[i].z = transform.position.z;
					verticesByNormals.Add(verticesTemp[i]);
					}		
				}
			getPolys = false;
			// feed all vertices into angle comparison function
			AssignVerticesAngles(verticesByNormals);
			// use the lowest angle as point of reference for poly
			polyTransform.Add (verticesListTemp[0]);
			verticesListTemp.Clear();
			verticesByNormals.Clear ();
			polyNumber++;
			}
		getPolys = true;
		AssignVerticesAngles(polyTransform);
		getPolys = false;
		// re-assigning polys in CW order
		for (int iG = 0; iG< walls.Length; iG++)
		{
		polygon[iG] = polygonNumber[iG];
		}		
	
	}
	
	// establishes vertices of all shadow casting objects in scene, check to see which vertices are visible by player
	public void DetectObjectVertices()				
	{			
		// number of useful vertices in scene
		verticesArraySize = 0;
		
		// cycle through walls, harvesting useful vertices					
		for (int _polyNumber = 0; _polyNumber<walls.Length; _polyNumber++)
			{						
			// highlight polygon being examined in red
//						polygon[polyNumber] = wall.gameObject;
			polygon[_polyNumber].renderer.material.color = Color.red;						
			// get mesh of current polygon
			Mesh meshHit = polygon[_polyNumber].GetComponent<MeshFilter>().mesh;	
			// determine size of vertices array by defining how many vertices are facing down (i.e. the bottom of the polygon) using normals
			normals = meshHit.normals;
			verticesTemp = meshHit.vertices;						
			for (int i=0; i<meshHit.normals.Length ; i++)
				{
				if (normals[i].z == 1)
					{
					verticesByNormals.Add(verticesTemp[i]);
					}		
				}						
									
			verticesTemp = verticesByNormals.ToArray();						
			// assign size of vertices array, +1 to include player position
			vertices = new Vector3[verticesByNormals.Count+1];
			for (int i=1; i<verticesByNormals.Count+1; i++)
				{							
				// adjust vertices for scale of poly
				vertices[i] = new Vector3 (verticesTemp[i-1].x*polygon[_polyNumber].transform.lossyScale.x, verticesTemp[i-1].y*polygon[_polyNumber].transform.lossyScale.y,transform.position.z);
				// adjust for world position
				vertices[i] = polygon[_polyNumber].transform.position - vertices[i];
				vertices[i].z = transform.position.z;							
//							// check to see if it's on the opposite side of the poly to the player, leave the vertices if it is, add if it isn't
				Vector3 rayPos = transform.position;
				RaycastHit hit;	
				if (!Physics.Linecast (rayPos, vertices[i], out hit)) 
								{
								_verticesList.Add (vertices[i]);
								Debug.DrawLine (vertices[i],rayPos,Color.green);											
								}							
				}						
//						Debug.Log (_verticesList.Count);
			AssignVerticesAngles(_verticesList);						
			AddVerticesToList();
			_verticesList.Clear();
			verticesByNormals.Clear();
			}
//						_marker = GameObject.FindGameObjectsWithTag("Walls");						
//						AssignVerticesAngles(verticesList);	
//						Debug.Log("hi");
		
			AssignVerticesAngles(verticesList);			
//			verticesList = verticesListTemp;
			
			DrawMesh();	
		}
	
	//establish vertices angle in relation to player, sort in clockwise order
	public void AssignVerticesAngles(List<Vector3> verticesAngles)
	{
	
	actualAngle = new float[verticesAngles.Count];	
	for (int i=0; i<verticesAngles.Count; i++)
		{
		// find angle of vertices
		verticesDirection = verticesAngles[i] - transform.position;
		verticesAngle = Vector3.Angle(verticesDirection,Vector3.up);		
		float dirNum = AngleDir(Vector3.up, verticesDirection, Vector3.forward); 		
			if(dirNum>0F)
				{
				actualAngle[i] += 360F-verticesAngle;				
				}
				else
				{
				actualAngle[i] += verticesAngle;
				}			
		}		
	//rearrange vertices into order moving clockwise around player	
	float sortCheckSize = actualAngle.Length;
	int iV = 0;	
	while (sortCheckSize > 0)
		{	
		while (actualAngle[iV] == 361)
			{
			iV++;
			}
		if (actualAngle[iV] != 361)
			{
			tempval = actualAngle[iV];	
			}
		 for (int iN=0; iN<actualAngle.Length;iN++)
			{					
			if ((tempval< actualAngle[iN]) || (tempval== actualAngle[iN]))
				{
					if (iN == actualAngle.Length-1)
					{
					// when function is being used for defining vertices
					if (getPolys== false)
						{
//						Debug.Log("hi hi");
						verticesListTemp.Add (verticesAngles[iV]);
						}
					else
					// when function is being used for defining polys
						{
//						Debug.Log("hi ho");
						polygonNumber.Add (polygon[iV]);
						}
					sortCheckSize -= 1;
					actualAngle[iV] = 361;
					iV = 0;
					}
				}
			if (tempval> actualAngle[iN])
				{
				iV++;
				iN = actualAngle.Length-1;				
				}
			}			
        }
		
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
	
	
	//(playerPos + surfacePos) * desiredShadowLength
	// extrude vertices of lit edges of poly (if it's visible), add both original and extruded value to vector list
	public void AddVerticesToList()
	{
		
		if (_verticesList.Count>0)
		{
//		Debug.Log("hi ho ho");
		Vector3 _verticesListTemp = verticesListTemp[0]; //original
		RaycastHit hit;
		if (Physics.Raycast(verticesListTemp[0], (verticesListTemp[0]- transform.position), out hit))
			{
            verticesListTemp[0] = hit.point;
			Debug.DrawLine (_verticesListTemp,verticesListTemp[0],Color.red);
			}
		else
			{
			verticesListTemp[0] = verticesListTemp[0]+((verticesListTemp[0]- transform.position)*Shadowlength); // extruded
			Debug.DrawLine (_verticesListTemp,verticesListTemp[0],Color.red);
			}
		verticesList.Add(verticesListTemp[0]);
		verticesList.Add(_verticesListTemp);
		_verticesListTemp = verticesListTemp[verticesListTemp.Count-1];
		if (Physics.Raycast(verticesListTemp[verticesListTemp.Count-1], (verticesListTemp[verticesListTemp.Count-1]- transform.position), out hit))
			{
            verticesListTemp[verticesListTemp.Count-1] = hit.point;
			Debug.DrawLine (_verticesListTemp,verticesListTemp[verticesListTemp.Count-1],Color.red);
			}
		else
			{
			verticesListTemp[verticesListTemp.Count-1] = verticesListTemp[verticesListTemp.Count-1]+ ((verticesListTemp[verticesListTemp.Count-1]- transform.position)*Shadowlength);
			Debug.DrawLine (_verticesListTemp,verticesListTemp[verticesListTemp.Count-1],Color.red);
			}
		verticesList.Add(_verticesListTemp);
		verticesList.Add(verticesListTemp[verticesListTemp.Count-1]);
		// check incase last vertices has swapped with third	
			
	
		}
		verticesListTemp.Clear();
		verticesAngles.Clear ();
		
	}	
	
	
	// create mesh for main lightmesh
	void DrawMesh()		
	{	
	Mesh mesh = meshHolder.GetComponent<MeshFilter>().mesh;	
	finalVertices = new Vector3 [verticesList.Count+1];	
	// assign triangles	
	triangles = new int [verticesList.Count*3];	
	for(int v = 1, t = 1; v < verticesList.Count+1; v++, t += 3)
		{				
			finalVertices[v-1] = verticesList[v-1];
			// make sure lightmesh is flat
			Vector3 tempvert = finalVertices[v-1];
			tempvert.z = transform.position.z;
			finalVertices[v-1] = tempvert;
			triangles[t] = v;
			triangles[t + 1] = v + 1;
		}
		triangles[triangles.Length-1] = 1;		
		
		finalVertices[verticesList.Count] = finalVertices[0];
		finalVertices[0] = transform.position;		
		mesh.vertices = finalVertices;
	
		mesh.triangles = triangles;		
		verticesList.Clear ();
		verticesListTemp.Clear();

		
	}
}