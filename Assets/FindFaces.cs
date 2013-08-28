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
public float[] testArray;
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
float vertexAngle;
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
//		walls = GameObject.FindGameObjectsWithTag("Walls");
//		ScanForObjects();
		DetectObjectVertices();	
		}
	
	
	public void ScanForObjects()						// finds objects in scene, arranges into CW order
	{
		// initiate array to include all polygons tagged 'wall'					
		polygon = new GameObject[walls.Length];
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
			
			CompareVerticesAngles(verticesByNormals);				// feed all vertices into angle comparison function
			
			polyTransform.Add (verticesListTemp[0]);				// assign the lowest angle as point of reference for poly
			verticesListTemp.Clear();
			verticesByNormals.Clear ();
			polyNumber++;
			}
		getPolys = true;
		CompareVerticesAngles(polyTransform);
		getPolys = false;		
		for (int iG = 0; iG< walls.Length; iG++) 					// re-assigning polys in CW order
		{
		polygon[iG] = polygonNumber[iG];
		}		
	
	}
	
	
	public void DetectObjectVertices()					// establishes vertices of all shadow casting objects in scene, check to see which vertices are visible by player		
	{			
		// number of useful vertices in scene
		verticesArraySize = 0;
		// cycle through walls, harvesting useful vertices					
		for (int _polyNumber = 0; _polyNumber<walls.Length; _polyNumber++)
			{						
			// highlight polygon being examined in red
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
//				// check to see if it's on the opposite side of the poly to the player, leave the vertices if it is, add if it isn't
				Vector3 rayPos = transform.position;
				RaycastHit hit;	
				if (!Physics.Linecast (rayPos, vertices[i], out hit)) 
								{
								_verticesList.Add (vertices[i]);
								Debug.DrawLine (vertices[i],rayPos,Color.green);											
								}							
				}						

				CompareVerticesAngles(_verticesList);	// arrange polygon's vertices into CW order
				AddVerticesToList(); 					// add to global vertices list
				_verticesList.Clear(); 					
				verticesByNormals.Clear();
			}
			
//			AddBoundaryPoints(); 						// add the perimeter points	
			CompareVerticesAngles(verticesList);			// arrange global vertices list into CW order		
			verticesList.Clear ();
//			Debug.Log(""+verticesList.Count+" 174");
			for (int iP = 0; iP<verticesListTemp.Count; iP++)
			{
			verticesList.Add (verticesListTemp[iP]);			
			}			
			
			DrawMesh();	
		}
	
	public void AddBoundaryPoints()						// add perimeter points if visible
	{
		Vector3[] boundaryPoint = new Vector3[4];
		boundaryPoint [0] = new Vector3 (-300,200, transform.position.z);
		boundaryPoint [1] = new Vector3 (300,200, transform.position.z);
		boundaryPoint [2] = new Vector3 (300,-200, transform.position.z);
		boundaryPoint [3] = new Vector3 (-300,-200, transform.position.z);
		RaycastHit hit;	
		for (int iBP = 0; iBP< boundaryPoint.Length; iBP++)
			{
			if (!Physics.Linecast (transform.position, boundaryPoint[iBP], out hit)) 
							{
							verticesList.Add (boundaryPoint[iBP]);
							Debug.DrawLine (boundaryPoint[iBP],transform.position,Color.blue);											
							}	
			}
	}	
	
	
	public void CompareVerticesAngles(List<Vector3> verticesAngles)			//establish vertices angle in relation to player, sort in clockwise order
	{
	// -part 1: assign angles
	actualAngle = new float[verticesAngles.Count];	
	for (int i=0; i<verticesAngles.Count; i++)
		{
		FindVertexAngle(verticesAngles[i]);
//		Debug.Log(vertexAngle);
		actualAngle[i] = vertexAngle;				
		}		
	// -part 2: compare to other angles
	float sortCheckSize = actualAngle.Length;
//	testArray = new float[actualAngle.Length];
//	for (int iP = 0; iP<actualAngle.Length; iP++)
//			{
//			testArray[iP] = actualAngle[iP];			
//			}	
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
					if (getPolys== false)				// when function is being used for defining vertices
						{
						verticesListTemp.Add (verticesAngles[iV]); 			// if angle is smallest, add corresponding vertex to global list
						}
					else 								// when function is being used for defining polys
						{
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
	
	public void FindVertexAngle (Vector3 vertexCoord)					// used to find angle of single vertex
	{		
		vertexAngle = 0;
		verticesDirection = vertexCoord - transform.position;
		float _angle = Vector3.Angle(verticesDirection,Vector3.up);		
		float dirNum = AngleDir(Vector3.up, verticesDirection, Vector3.forward); 		
			if(dirNum>0F)
				{
				vertexAngle += 360F-_angle;				
				}
				else
				{
				vertexAngle += _angle;
				}
//		Debug.Log (vertexAngle);
	}	
	
		private float AngleDir(Vector3 fwd, Vector3 targetDir, Vector3 up) //variable to turn acute 180 degree angles into 360 degree angles
			{ 
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
	public void AddVerticesToList() 					// adding vertices of poly to global vertices list 
	{		
		if (verticesListTemp.Count>0)
		{
		Vector3 _verticesListTemp = verticesListTemp[0]; //assigning first clockwise vertex of poly to temp value
		RaycastHit hit;
		if (Physics.Raycast(verticesListTemp[0], (verticesListTemp[0]- transform.position), out hit)) 			// raycasting to wall behind poly, if a hit, use that point...
			{
            verticesListTemp[0] = hit.point;				
			FindVertexAngle(verticesListTemp[0]); 		// moving raycasted point a smidgeon, so as not to interfere with _verticesTemp (otherwise they have same angle when it comes to CW sorting)
			vertexAngle = 360 - vertexAngle + 90 + 1f; 				
			Vector3 vertNudge = verticesListTemp[0];
			float vertexDist = Vector3.Distance(_verticesListTemp, hit.point); 
			float addShadX = _verticesListTemp.x + vertexDist * Mathf.Cos(vertexAngle * Mathf.PI / 180);
			float addShadY = _verticesListTemp.y + vertexDist * Mathf.Sin(vertexAngle * Mathf.PI / 180);
			vertNudge = new Vector3(addShadX, addShadY, vertNudge.z);
			verticesListTemp[0] = vertNudge;
			Debug.DrawLine (_verticesListTemp,verticesListTemp[0],Color.red);
			}
		else
			{
			verticesListTemp[0] = verticesListTemp[0]+((verticesListTemp[0]- transform.position)*Shadowlength); // if not, extrude point an arbitrary distance (Shadowlength)
			FindVertexAngle(verticesListTemp[0]); 
			vertexAngle = 360 - vertexAngle + 90 + 1f; 
			Vector3 vertNudge = verticesListTemp[0];
			float vertexDist = Vector3.Distance(_verticesListTemp, verticesListTemp[0]); 
			float addShadX = _verticesListTemp.x + vertexDist * Mathf.Cos(vertexAngle * Mathf.PI / 180);
			float addShadY = _verticesListTemp.y + vertexDist * Mathf.Sin(vertexAngle * Mathf.PI / 180);
			vertNudge = new Vector3(addShadX, addShadY, vertNudge.z);
			verticesListTemp[0] = vertNudge;
			Debug.DrawLine (_verticesListTemp,verticesListTemp[0],Color.red);
			}
		verticesList.Add(verticesListTemp[0]);			// add extruded poly vertex to global vertex list
		verticesList.Add(_verticesListTemp);			// add actual vertex of poly to global vertex list
			
		_verticesListTemp = verticesListTemp[verticesListTemp.Count-1]; // same process again, but with last CW vertex of poly, and the points are added in reverse (extruded poly point last), to aid mesh creation
		if (Physics.Raycast(verticesListTemp[verticesListTemp.Count-1], (verticesListTemp[verticesListTemp.Count-1]- transform.position), out hit))
			{
            verticesListTemp[verticesListTemp.Count-1] = hit.point;			 
			FindVertexAngle(verticesListTemp[verticesListTemp.Count-1]); 
			vertexAngle = 360 - vertexAngle + 90 - 1f; 
			Vector3 vertNudge = verticesListTemp[verticesListTemp.Count-1];
			float vertexDist = Vector3.Distance(_verticesListTemp, hit.point); 
			float addShadX = _verticesListTemp.x + vertexDist * Mathf.Cos(vertexAngle * Mathf.PI / 180);
			float addShadY = _verticesListTemp.y + vertexDist * Mathf.Sin(vertexAngle * Mathf.PI / 180);
			vertNudge = new Vector3(addShadX, addShadY, vertNudge.z);
			verticesListTemp[verticesListTemp.Count-1] = vertNudge;
			Debug.DrawLine (_verticesListTemp,verticesListTemp[verticesListTemp.Count-1],Color.red);
			}
		else
			{			
			verticesListTemp[verticesListTemp.Count-1] = verticesListTemp[verticesListTemp.Count-1]+((verticesListTemp[verticesListTemp.Count-1]- transform.position)*Shadowlength); 
			FindVertexAngle(verticesListTemp[verticesListTemp.Count-1]); 
			vertexAngle = 360 - vertexAngle + 90 - 1f; 
			Vector3 vertNudge = verticesListTemp[verticesListTemp.Count-1];
			float vertexDist = Vector3.Distance(_verticesListTemp, verticesListTemp[verticesListTemp.Count-1]); 
			float addShadX = _verticesListTemp.x + vertexDist * Mathf.Cos(vertexAngle * Mathf.PI / 180);
			float addShadY = _verticesListTemp.y + vertexDist * Mathf.Sin(vertexAngle * Mathf.PI / 180);
			vertNudge = new Vector3(addShadX, addShadY, vertNudge.z);
			verticesListTemp[verticesListTemp.Count-1] = vertNudge;
			Debug.DrawLine (_verticesListTemp,verticesListTemp[verticesListTemp.Count-1],Color.red);
			}
		verticesList.Add(_verticesListTemp);
		verticesList.Add(verticesListTemp[verticesListTemp.Count-1]);	
		}
		verticesListTemp.Clear();
		verticesAngles.Clear ();
		
	}	
	
	void DrawMesh()		// create mesh for main lightmesh
	{	
	Mesh mesh = meshHolder.GetComponent<MeshFilter>().mesh;
//	Debug.Log(""+verticesList.Count+" 309");
	finalVertices = new Vector3 [verticesList.Count+1];	
	// assign triangles	
	triangles = new int [verticesList.Count*3];	
	for(int v = 1, t = 1; v < verticesList.Count+1; v++, t += 3)
		{				
			finalVertices[v] = verticesList[v-1];
			// make sure lightmesh is flat
			Vector3 tempvert = finalVertices[v-1];
			tempvert.z = transform.position.z;
			finalVertices[v-1] = tempvert;
			triangles[t] = v;
			triangles[t + 1] = v + 1;
		}
		triangles[triangles.Length-1] = 1;		
		
//		finalVertices[verticesList.Count] = finalVertices[0];
		finalVertices[0] = transform.position;		
		mesh.vertices = finalVertices;
//		Debug.Log(""+verticesList.Count+" 329");
		mesh.triangles = triangles;		
		verticesList.Clear ();
		verticesListTemp.Clear();

		
	}
}