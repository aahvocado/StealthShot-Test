using UnityEngine;
using System.Collections;
//List Usage
using System.Collections.Generic;

/*	GeometricVisibility
	
	After countless bug fix attemps of my previous scripts i took a shot on this example:
		http://www.redblobgames.com/articles/visibility/
	
	The performance may not be the very best
		-there are cheaper angle compare algorithms (pseudo angle) if we split the hemispheres
		-we can add a predrop phase of whole segments
		-and lots of stuff i don't know, if you find something pls let me know
		
		
	Additionally to the plain visibility algorithm i added Polygon extraction, Fadeeffect for invisible objects, mesh generation
	and extrusion to create a cake like visibility polygon that is able to occlude objects in a 2.5D environment.
	
	HOW IT WORKS:	
	
	>Attach the script to any GO
	>choose a GO as center of visibility
	>Tag your wall objects "Wall" for static objects
		>make sure that your walls have a flat bottom face facing in -y direction or adjust the if(....normals[i].y=-1) to your needs
		>choose between
			>visibilityPolygon
			>inverted visibilityPolygon (surrounding rect or circle with the visibility polgon cut out of it)
			>extrusion of the polygon (making it a prism for usage in 2.5D environment)


	Remarks:
	- scripts are c#
	- only walls that are visible in a viewport (Renderer.isVisible) are used in the calculation to improve performance
	- optionally you can manually choose which walls are included in the calculation by supplying a rectangle of the area (i need this because i use a splitscreen setup that may have to include objects between the 2 cameras that aren't visible)
	- mutliple visibilityPolygons are no problem - if you have the performance
	- it cannot meaningful support mutliple inverted visibilityPolygons for obvious reasons (the invisible areas would add up instead of the visible)
	- it is manufactured for top-down environments (all points on X-Z-plane) if you want it for a side scroller (e.g. X-Y-Plane) you only have to insert the z-values as y values before calculation and read the output.y as z-value

				
	-marrt
	marrt@gmx.at or post in the thread
	
	
	My first attempts to convert a c++ code serumas handed to me failed.
		http://serumas.gamedev.lt/index.php?id=visibility
		see GeometricVisibilityOLD if you are interested
			-disable GeometricVisibility on CameraO
			-enable GeometricVisibilityOLD on CameraO

*/

public class GeometricVisibility : MonoBehaviour {
	
	#region	VARIABLES
	
	private	Camera cam;			//camera, for panning zooming etc.
	public	Transform source;	//the Transform of the lightSource, for position setting/getting
	public	Transform testSphere;
	
	//shadow caster GameObjects
	GameObject[] walls;
		
	private List<Vector3> segmentLines;
	private List<int> owners;//owners of segmentLines, determines visibility of an object, needed for the fader in future
	
	//polygons as struct allowing to have objects references
	private BottomPolygon[]	staticBlockers; //immoveable walls
	//private BottomPolygon[]	dynamicBlockers; //dynamic blockers, recalc positions for relevance
	
	private BottomPolygon[]	randomPolys; //immoveable walls
	
	private struct BottomPolygon{
		//defining ordering: CCW viewed from top
		
		//vertices of this polygon, Vector2 would be ok too
		public Vector3[] vertices;
								
		//the center position
		public Vector3 position;
		
		//radius around center where this polygon has to be taken into calculation
		public float relevantRadius;
		
		//the corresponding fader of the wall object that fades out currently invisible walls... future
		public VisibilityFader fader;
		
		//corresponding Transform to recalc vertices in WorldCoordinates if needed
		public Transform transform;
				
		//corresponding Renderer, used when determining if polys are to be included in teh calculation: Renderer.isVisible
		public Renderer renderer;		
		
		//flag for including this poly in the calculation
		public bool include;
	}
	
	
	//GUI Variables
					
	//VISIBILITY
	private	bool extrude			= false;
	private	bool invert				= false;
	
	private	float perimeter = 300F;	//outer perimeter, should be larger than vieport diameter /2 when used ingame	
	private	float exHeight = 2.5F;
	
	//GATHERING
	private	bool cheapGather		= true;		//predrop invisible sides of the mesh
	private	bool inclPerRenderer	= false;	//check per Renderer.isVisible if including Polygon
	private	bool inclPerRadius		= false;	//predrop invisible sides of the mesh
	private	float inclRadius		= 50F;
	private	bool inclBoundary		= true;	//include map boundary
	private	bool roundBoundary		= false;//include map boundary
	private	float roundBRadius		= 50F;
	private	float lastRBRadius		= 0F;	//triggers InitRoundBounds if not the same as above
	
	//GENERATE
	private	bool checkCCW			= true;
	private	bool worldNormals		= false;
	private	bool invisibleFaces		= false;
	private	bool autoGen			= false;
	private	bool clampY				= false;
	
	//DRAW
	private	bool drawPolygons	= false;
	private	bool drawOutput		= false;
	
	private	float pi			= Mathf.PI;
	private	float pi2			= 2F*Mathf.PI;
	
	
	//Presentation of Gameobjects
	public	GameObject	cubeParent;
	public	Material	darkness;
	public	Material	light;
		
	#endregion	
	
	#region	STARTUP & UPDATE
	
	//called once after game starts
	private	void Awake(){
		cam = transform.camera;
	}
	
	//called once after Awake
	private	void Start(){
		walls = GameObject.FindGameObjectsWithTag("Wall");
		segmentLines = new List<Vector3>();
		
		//Struct
		GeneratePolygonStructArr(ref staticBlockers);
		
		//init randPolys
		ClearRandomPolygons(ref randomPolys);
		
		StartCoroutine(FrameRateUpdate());
		ToggleVsync();
		FaderTest(ref staticBlockers);//starts with a fade in
	}
	
	//called every Frame
	private	void Update () {		
		CameraManipulation();
		polyCount = randomPolys.Length;
		
		//empty list and Update it
		segmentLines.Clear();		
					
		Flush(); //empty algorithm variables
		
		if(cheapGather){
			
			if(inclPerRenderer){
				CheckInclusionPerRenderer(ref staticBlockers);	//checks render.isVisible for inclusion
			}else if(inclPerRadius){
				CheckInclusion(ref staticBlockers, source.position, inclRadius);
				CheckInclusion(ref randomPolys, source.position, inclRadius);
			}//else just take all polys into account
			
			CheapGather(ref staticBlockers);
			CheapGather(ref randomPolys);
			
			linecount2ndApp = 0;
			for(int i = 0; i<segmentLines.Count; i+=2){
				addSegment(segmentLines[i].x, segmentLines[i].z, segmentLines[i+1].x, segmentLines[i+1].z);
				linecount2ndApp++;
			}			
		}else{ //full pass of all edges
			foreach(BottomPolygon b in staticBlockers){
				float x = source.position.x;
				float y = source.position.z;
				for(int i = 0; i<b.vertices.Length-1; i++){
					addSegment(b.vertices[i].x-x, b.vertices[i].z-y, b.vertices[i+1].x-x, b.vertices[i+1].z-y);
				}
				addSegment(b.vertices[b.vertices.Length-1].x-x, b.vertices[b.vertices.Length-1].z-y, b.vertices[0].x-x, b.vertices[0].z-y);
			}
			foreach(BottomPolygon b in randomPolys){
				float x = source.position.x;
				float y = source.position.z;
				for(int i = 0; i<b.vertices.Length-1; i++){
					addSegment(b.vertices[i].x-x, b.vertices[i].z-y, b.vertices[i+1].x-x, b.vertices[i+1].z-y);
				}
				addSegment(b.vertices[b.vertices.Length-1].x-x, b.vertices[b.vertices.Length-1].z-y, b.vertices[0].x-x, b.vertices[0].z-y);
			}
		}
		
		InitCalc(source.position);
		
		
		
		
				
		if(autoGen)				{ CreateRandomPolygon(ref randomPolys);}						
		//orthogonal cross: center of map
		Debug.DrawLine(new Vector3(-250F,0F,0F),new Vector3(+250F,0F,0F),new Color(1F,1F,1F,0.09F));
		Debug.DrawLine(new Vector3(0F,0F,-250F),new Vector3(0F,0F,+250F),new Color(1F,1F,1F,0.09F));
		//orthogonal cross: center of light source
		Debug.DrawLine(new Vector3(-250F,0F,source.position.z),new Vector3(+250F,0F,source.position.z),new Color(1F,1F,1F,0.075F));
		Debug.DrawLine(new Vector3(source.position.x,0F,-250F),new Vector3(source.position.x,0F,+250F),new Color(1F,1F,1F,0.075F));
	}
	#endregion
			
	#region	POLYGON EXTRACTION
	private	void GeneratePolygonStructArr(ref BottomPolygon[] poly){
		poly = new BottomPolygon[walls.Length];
		
		int iPoly = 0;	//polygon integrator
		foreach(GameObject wall in walls){
			
			//Save Transform reference
			poly[iPoly].transform = wall.transform;
			poly[iPoly].renderer = wall.GetComponent<Renderer>();
			
			Mesh mesh = wall.GetComponent<MeshFilter>().mesh;
			Vector3[] vertices = mesh.vertices;
			int[] triangles = mesh.triangles;
			
			Vector3[] normals = mesh.normals;
			
			if(worldNormals){
				for(int i = 0; i < vertices.Length; i++){
					normals[i] = poly[iPoly].transform.TransformDirection(mesh.normals[i]);
				}
			}		
					
		//SIZECHECK, list usage could get rid of this step
			//check how much valid vertex are present to assign array lengths in the next step
			int validVertices = 0;
			for(int i = 0; i < vertices.Length; i++){
				//BOTTOM, or which orthogonal direction you need... e.g. sidescroller: if mesh.normals.z-1
				if(normals[i].y == -1){	//if the normal of this vertice is pointing down
					validVertices++;
				}
			}
									
		//BOTTOM-VERTICES of the walls bottom-plane
			poly[iPoly].vertices = new Vector3[validVertices];	//init new Vector3 array of the struct with the needed length
			int[] validIndices = new int[validVertices];					//the original indices of the valid vertices, used to find the right triangles
			Vector3[] bottomVertices = new Vector3[validVertices];
			//int[] newIndices = new int[validVertices];						//new indices of the vertices, used to map newTriangles
			
			
			//save the valid vertices and triangles of the current wall
			int iv = 0;	//array integrator
			for(int i = 0; i < vertices.Length; i++){	//for ALL vertices of the wall mesh
				if(normals[i].y == -1){	//if the normal of this vertice is pointing down, e.g. should be only 4 vertices per cube
					//actual saving of the vertex in WORLD COORDINATES
					bottomVertices[iv] = wall.transform.TransformPoint(vertices[i]);
					validIndices[iv] = i;
					//newIndices[iv] = iv;
					iv++;
				}
			}
			
			if(validIndices.Length == 0){break;}//early out
		
		//BOTTOM-TRIANGLES of one poly, maybe we dont need them directly later, but here they are needed to delete inner vertices (e.g. center of cylinder vertex)
			List<int> bottomTrianglesList = new List<int>();	//using the OLD indices
			int iAs = 0; //iterator for assigned triangles
			for(int it = 0; it < triangles.Length;){// iterator triangles
				//check if the next 3 indices of triangles match
				int match = 0;//check the next 3 indices of this triangles
				for(int imatch = 0; imatch<3; imatch++){
					for(int ivv = 0; ivv < validIndices.Length; ivv++){//check with all vertices
						if(validIndices[ivv]==triangles[it+imatch]){
							match++;
						}
					}
				}
				//if all 3 indices of a triangle match with the validIndices, it is a bottom triangle
				if(match == 3){ //create new triangle in list
					bottomTrianglesList.Add(triangles[it+0]);
					bottomTrianglesList.Add(triangles[it+1]);
					bottomTrianglesList.Add(triangles[it+2]);
					iAs += 3; //assign iterator rdy for next triangle
				}
				it+=3;//next triangle
			}
			//now we have all triangles that are contained in the bottom plane, but with the original indices
						
			int[] bottomTrianglesArr = bottomTrianglesList.ToArray();
			int[] bottomTriangles = new int[bottomTrianglesArr.Length];	//using the OLD indices
			//Update indices to refer to bottomVertices:
			for(int ib = 0; ib < bottomTrianglesArr.Length; ib++){
				for(int ivi = 0; ivi < validIndices.Length; ivi++){	//check for original index, assign corresponding new index, must hit once per loop!
					if(bottomTrianglesArr[ib] == validIndices[ivi]){
						bottomTriangles[ib] = ivi;//currently the same as newTriangles[ib] = newIndices[ivi];, we dont need newIndices[]
					}
				}
			}
					
//AT THIS POINT we have the bottom vertices and triangles of the bottomPlane rdy for any use
//			bottomVertices & bottomTriangles
			
			//Now we have to find the outlining polygon:			
			ExtractPolygon(bottomVertices, bottomTriangles, ref poly[iPoly]);	//extracts polygon and saves it directly in passed poly struct
			
			
		//OTHER assignments for future purpose
			//add and save visibilityFader Reference and set Blackness of the Fader
			if(!wall.GetComponent<VisibilityFader>()){
				poly[iPoly].fader = wall.AddComponent<VisibilityFader>();
				poly[iPoly].fader.ManualInit();
			}else{
				poly[iPoly].fader = wall.GetComponent<VisibilityFader>();
			}

			poly[iPoly].fader.SetBlackness(0.0F);			//set lower fadeout boundary
			poly[iPoly].fader.EnableTransparentFade(true);	//transparent or black obstacles on fadeout
			
			poly[iPoly].include = true;
			
			CalculateCenterAndRadius(ref poly[iPoly]);
		//OTHER END

			iPoly++;
		}
	}
	
	//GeneratePolygonStructArr-helper, deletes inner vertices, returns new vertices, no triangles because it would make no sense without inner vertices
	private Vector3[] DeleteInnerVertices(Vector3[] vertices, int[] triangles){
		List<Vector3> outerVertices = new List<Vector3>();
		for(int iv = 0; iv<vertices.Length; iv++){//for all vertices
			int matches = 0;
			for(int it = 0; it < triangles.Length; it++){
				if(triangles[it] == iv){//check how often this vertex-index appears in the triangles
					matches++;
				}
			}
			//Assumption:
			if(matches < 3){//if vertex is used in less than 3 triangles
				//then this is an outer vertex, add it to list
				outerVertices.Add(vertices[iv]);
			}
		}
		//create array
		return outerVertices.ToArray();
	}
	
	private void ExtractPolygon(Vector3[] vertices, int[] triangles, ref BottomPolygon poly){		
		//definitions used (see http://www.geosensor.net/papers/duckham08.PR.pdf)
		//→A triangulation ∆ is a combinatorial map which has the property that every edge in a set of edges belongs to either one or two triangle s
		//→Aboundary edge of ∆ is an edge that belongs to exactly one triangle in ∆.
		
		//for simple polygons(edges do not cross themselfes) which we are dealing with,
		//the outline polygon consists out of the edges that appear only in one triangle:
		
		//earlyOutTest
		//poly.vertices	= vertices;	return;
		
		List<int[]> allEdges = new List<int[]>();					//list of 2 integers each representing the index of a vertex
		List<int[]> unsortedBE = new List<int[]>();					//unsortedBoundaryEdges
		List<int[]> boundaryEdges = new List<int[]>();				//sorted outer edges
		
		List<Vector3> boundaryVertices = new List<Vector3>();	//the vertices of the polygon in ccw order, this is what we need!
		
		//get all edges
		for(int it = 0; it<triangles.Length;){//for all triangles, add their adges to the list
			allEdges.Add(new int[2]{triangles[it+0],triangles[it+1]});	//edge1
			allEdges.Add(new int[2]{triangles[it+1],triangles[it+2]});	//edge2
			allEdges.Add(new int[2]{triangles[it+2],triangles[it+0]});	//edge3
			it+=3;
		}//Debug.Log("Edges:"+allEdges.Count);
		
		//DROP all edges that appear in more than one triangle
		for( int iT = 0; iT < allEdges.Count; iT++){	//for each edge
			int o = iT%3;	//offset to find the edges that belong to the same triangle
			bool addEdge = true;
			for(int iC = 0; iC < allEdges.Count; iC++){	//compare loop, check all other edges
				if(	!((iT+0-o) == iC	||	(iT+1-o) == iC	||	(iT+2-o) == iC)	){	//except edges of current triangle from check
					if(		(allEdges[iT][0] == allEdges[iC][0] && allEdges[iT][1] == allEdges[iC][1]) || 
							(allEdges[iT][0] == allEdges[iC][1] && allEdges[iT][1] == allEdges[iC][0]) ){
						addEdge = false;
						break;
					}
				}
			}
			//if this edge has not appeared twice we can add it to our boundary edge List
			if(addEdge){	unsortedBE.Add(allEdges[iT]);	}
		}//Debug.Log("Edges:"+unsortedBE.Count);
		
		//SORT unsortedBE, no edge will be dropped now, indices of each edge may be swapped
		//→	unsorted List:
		//	edge1		edge2		edge3		edge4
		//	[4][2]		[0][1]		[1][4]		[0][2]
		
		//→	sorted List:
		//	edge1		edge2(4)	edge3(2)	edge4(3)
		//	[4][2]		[2][0]		[0][1]		[1][4]
				
		//add first edge to start:
		boundaryEdges.Add(unsortedBE[0]);
		int failsave = 100;	//if bottomplane is faulty we cannot create a closed loop
		for(int iList = 1; iList < unsortedBE.Count;){	//compare loop, one edge has to match each run (closed edge loop)
			for(int iC = 1; iC < unsortedBE.Count; iC++){	//check all edges but the first (already added)	
				//check if last index matches with another index, then add it to the sorted list
				//Debug.Log(boundaryEdges[iList-1][1] +"|"+ unsortedBE[iC][0]);
				if( boundaryEdges[iList-1][1] == unsortedBE[iC][0] ){	//common vertex on compare-edge[0], add!
					boundaryEdges.Add(new int[2]{unsortedBE[iC][0],unsortedBE[iC][1]});
					iList++;
				}else if( boundaryEdges[iList-1][1] == unsortedBE[iC][1] ){	//common vertex on compare-edge[1], add swapped!
					boundaryEdges.Add(new int[2]{unsortedBE[iC][1],unsortedBE[iC][0]});
					iList++;					
				}
			}
			
			if(failsave<1){
				Debug.Log("Aborted Loop, bottomplane of ["+poly.transform.name+"] has gap or is faulty!");
				break;
			}
			failsave--;
		}
			
		//Finally! generate the vertices of the polygon out of the sorted list
		foreach(int[] intArr in boundaryEdges){
			//just add one side([0] or [1]) of each element in the sorted List and we have the vertices in right order
			boundaryVertices.Add(vertices[intArr[0]]);
		}
		
		if(checkCCW){
			FixCCWOrder(ref boundaryVertices);
		}
		
		//put this in boundaryVertices assignment if always needed
		if(clampY){
			for(int iV = 0; iV<boundaryVertices.Count; iV++){
				boundaryVertices[iV] = new Vector3(boundaryVertices[iV].x,0F,boundaryVertices[iV].z);
			}
		}
				
		poly.vertices = boundaryVertices.ToArray();
	}
	
	//checking order of vertices via angle sum
	private	void FixCCWOrder(ref List<Vector3> vertices){
		//the list could be CW instead of CCW, check it by checking angle sum
		//→inner angles is always smaller than outer angles
		float angleSumInc = 0F;	//the angleSum of the polygon in one direction
		float angleSumDec = 0F;	//the angleSum of the polygon in the other direction
		for(int iV = 0; iV<vertices.Count; iV++){
			int nV	= (iV+1)%vertices.Count; //next vertice
			int nV2	= (iV+2)%vertices.Count; //next vertice
			//get Direction to next point, we need 2 direction to calc the angle in between
			Vector3 dir1 = (vertices[iV] -vertices[nV]);	//direction from next vertex to current Vertex
			Vector3 dir2 = (vertices[nV2]-vertices[nV]);	//direction from next vertex to next next Vertex
			//Debug.DrawRay(vertices[nV],dir1*1.2F,Color.red,0.5F,false);
			//Debug.DrawRay(vertices[nV],dir2*1.2F,Color.yellow,1F,false);
			float angle = Vector3.Angle(dir1,dir2);//always shortest angle in 3D space!
			//therefore check if we have a left turn or right turn
			if(AngleDir(dir1,dir2,Vector3.up)>0F){
				angleSumInc += angle;
				angleSumDec += 360F-angle;
			}else{
				angleSumInc += 360F-angle;
				angleSumDec += angle;
			}	
		}//Debug.Log("AngleSum:\t\tinc:\t"+angleSumInc +"\n\t\t\t\t\tdec:\t"+angleSumDec);
		
		//i order is reversed, fix it!
		if(angleSumInc>angleSumDec){vertices.Reverse();}
	}
	
	//same as above but for arrays rather than lists
	private	Vector3[] FixCCWOrder(Vector3[] vertices){
		float angleSumInc = 0F;
		float angleSumDec = 0F;
		for(int iV = 0; iV<vertices.Length; iV++){
			int nV	= (iV+1)%vertices.Length;
			int nV2	= (iV+2)%vertices.Length;
			Vector3 dir1 = (vertices[iV] -vertices[nV]);
			Vector3 dir2 = (vertices[nV2]-vertices[nV]);
			float angle = Vector3.Angle(dir1,dir2);
			if(AngleDir(dir1,dir2,Vector3.up)>0F){
				angleSumInc += angle;
				angleSumDec += 360F-angle;
			}else{
				angleSumInc += 360F-angle;
				angleSumDec += angle;
			}	
		}
		if(angleSumInc>angleSumDec){
			Vector3[] reversedVertices = new Vector3[vertices.Length];
			for(int iV = 0; iV<vertices.Length; iV++){
				reversedVertices[iV] = vertices[vertices.Length-iV-1];
			}
			return reversedVertices;
		}
		return vertices;
	}
	
	//average of all vertices does not need to be accurate, only not to small
	private	void CalculateCenterAndRadius(ref BottomPolygon polygon){
		
		float minX =  9000F;
		float maxX = -9000F;
		float minY =  9000F;
		float maxY = -9000F;		
		for(int i=0; i< polygon.vertices.Length; i++){
			if(polygon.vertices[i].x<minX)	{ minX = polygon.vertices[i].x;	}
			if(polygon.vertices[i].x>maxX)	{ maxX = polygon.vertices[i].x;	}
			if(polygon.vertices[i].z<minY)	{ minY = polygon.vertices[i].z;	}
			if(polygon.vertices[i].z>maxY)	{ maxY = polygon.vertices[i].z;	}
		}
			
		polygon.position		= new Vector3((minX+maxX)*0.5F,0F,(minY+maxY)*0.5F);			// center of max values
		polygon.relevantRadius	= (Mathf.Sqrt( (maxX-minX)*(maxX-minX)+(maxY-minY)*(maxY-minY) ))/2F;// perimeter
		
		Debug.DrawLine(new Vector3(minX,0F,minY), new Vector3(minX,0F,maxY), Color.blue, 1.5F, false);
		Debug.DrawLine(new Vector3(minX,0F,maxY), new Vector3(maxX,0F,maxY), Color.blue, 1.5F, false);
		Debug.DrawLine(new Vector3(maxX,0F,maxY), new Vector3(maxX,0F,minY), Color.blue, 1.5F, false);
		Debug.DrawLine(new Vector3(maxX,0F,minY), new Vector3(minX,0F,minY), Color.blue, 1.5F, false);
		
		Debug.DrawLine(	new Vector3(polygon.position.x-polygon.relevantRadius, 0F, polygon.position.z),
						new Vector3(polygon.position.x+polygon.relevantRadius, 0F, polygon.position.z), Color.cyan, 1.5F, false);
		Debug.DrawLine(	new Vector3(polygon.position.x, 0F, polygon.position.z-polygon.relevantRadius),
						new Vector3(polygon.position.x, 0F, polygon.position.z+polygon.relevantRadius), Color.cyan, 1.5F, false);
	}
	#endregion
		
	#region GATHER PROCESSING DATA	
	//cheaper 360° Gather for 2nd approach, without the top/bottom distinction and cut
	
	//gather relevant Polygons, check the polygon position increased by its radius if it is near our relevant radius
	//	↘viewport may be rotateable, for convenience we check if in radius not if within a maybe rotated rectangular area
	private	void CheckInclusion(ref BottomPolygon[] polys, Vector3 lightPosition, float radius){
		for(int iP =0; iP<polys.Length;iP++){
			//Vector3.magnitude or Distance is slower than sqrMag
			if( ((polys[iP].position-lightPosition).sqrMagnitude - polys[iP].relevantRadius*polys[iP].relevantRadius) < radius*radius){//faster than distance
			//if( Vector3.Distance(polys[iP].position, lightPosition) < radius){
				polys[iP].include = true;
			}else{
				polys[iP].include = false;
			}
		}
	}
	
	private	void CheckInclusionPerRenderer(ref BottomPolygon[] polys){
		for(int iP =0; iP<polys.Length;iP++){
			if( polys[iP].renderer.isVisible){
				polys[iP].include = true;
			}else{
				polys[iP].include = false;
			}
		}
	}
	
	//costum inclusion check for a game that uses splitscreen cameras
	//if the 2 cameras would be far apart the objects in between the 2 viewport rectangles would not be into the calculation (renderer.isVisible would be false)
	//so i include only those objects which extents lie inside the rectangle strip between the 2 viewports
	private	void CheckInclusionPerRectangle(ref BottomPolygon[] polys){
		//future	
	}
	
	
	private	void CheapGather(ref BottomPolygon[] poly){
		Vector3 off = source.position;
		for(int ip = 0; ip < poly.Length; ip++){	//polygon
			
			if(!poly[ip].include){continue;}
			
			//writing values into list "segmentLines"
			
			int length = poly[ip].vertices.Length;
			if(!invisibleFaces){
				for(int iv = 0; iv < length; iv++){	//vertices of the polygon
					//int nv = (iv<length-1)? iv+1 : 0; //next vertex
					int nv = (iv+1)%length;
					Vector3 vertexDirection = poly[ip].vertices[iv]-poly[ip].vertices[nv];
					Vector3 sourceDirection = source.position-poly[ip].vertices[iv];
										
					if( AngleDir(vertexDirection,sourceDirection,Vector3.up)<0F ){
						segmentLines.Add(poly[ip].vertices[iv]-off);
						segmentLines.Add(poly[ip].vertices[nv]-off);
					}
				}
			}else{
				for(int iv = 0; iv < length; iv++){	//vertices of the polygon
					int nv = (iv+1)%length;
					Vector3 vertexDirection = poly[ip].vertices[iv]-poly[ip].vertices[nv];
					Vector3 sourceDirection = source.position-poly[ip].vertices[iv];
										
					if( AngleDir(vertexDirection,sourceDirection,Vector3.up)>0F ){
						segmentLines.Add(poly[ip].vertices[iv]-off);
						segmentLines.Add(poly[ip].vertices[nv]-off);
					}
				}
			}
		}		
	}
	
	//check if a point is left or right to a direction vector, can be reduced for 2D only
	private	float AngleDir(Vector3 fwd, Vector3 targetDir, Vector3 up) {
		Vector3 perp = Vector3.Cross(fwd, targetDir);
		float dir	 = Vector3.Dot(perp, up);
		if		(dir > 0F)	{ return  1F;}//RIGHT
		else if	(dir < 0F)	{ return -1F;}//LEFT
		else				{ return  0F;}
	}		
	#endregion
		
	#region THE ACTUAL ALGORITHM
	//REDBOXGAMES
	// Calculate visible area from a position
	// Copyright 2012 Red Blob Games
	// License: Apache v2
	
	/*
	   This code also uses a linked list datastructure class from
	   Polygonal, which is Copyright (c) 2009-2010 Michael Baczynski,
	   http://www.polygonal.de. It is available under the new BSD license,
	   except for two algorithms, which I do not use. See
	   https://github.com/polygonal/polygonal/blob/master/LICENSE
	*/
	
//	import de.polygonal.ds.DLL;
	
//	typedef Block = {x:Float, y:Float, r:Float};
//	typedef Point = {x:Float, y:Float};
//	typedef Segment = {p1:EndPoint, p2:EndPoint, d:Float};
//	typedef EndPoint = {x:Float, y:Float, begin:Bool, segment:Segment, angle:Float, visualize:Bool};
	
	public class Block{
		public	float x;
		public	float y;
		public	float r;
	}
	
	public class Point{	//=vector2
		public	float x;
		public	float y;
	}
	
	public class Segment{
		public	EndPoint p1;
		public	EndPoint p2;
		public	float d;
	}
	
	public class EndPoint{
		public	float x;
		public	float y;
		public	bool  begin;
		public	Segment segment;
		public	float angle;
		public	bool visualize;
		
		/*public void Init(Point p1, Point p2, bool  begin, Segment segment, float angle, bool visualize){
			this.p1 = p1;
			this.p2 = p2;
			this.begin = begin;
			this.segment = segment;
			this.angle = angle;
			this.visualize = visualize;
		}*/
	}
	
	/* 2d visibility algorithm, for demo
	   Usage:
		  new Visibility()
	   Whenever map data changes:
		  loadMap
	   Whenever light source changes:
		  setLightLocation
	   To calculate the area:
		  sweep
	*/
	
//	@:expose @:keep class Visibility {
		// Note: DLL is a doubly linked list but an array would be ok too
	
		// These represent the map and the light location:
//		public var segments:DLL<Segment>;
		private	List<Segment> segments = new List<Segment>();
//		public var endpoints:DLL<EndPoint>;
		private	List<EndPoint> endpoints = new List<EndPoint>();
//		public var center:Point;
		private	Point center;
	
		// These are currently 'open' line segments, sorted so that the nearest
		// segment is first. It's used only during the sweep algorithm, and exposed
		// as a public field here so that the demo can display it.
//		public var open:DLL<Segment>;
		private	List<Segment> open = new List<Segment>();

		// The output is a series of points that forms a visible area polygon
//		public var output:Array<Point>;
		private List<Point> output = new List<Point>();
	
		// For the demo, keep track of wall intersections
//		public var demo_intersectionsDetected:Array<Array<Point>>;
//		private List<List<Point>> demo_intersectionsDetected = new List<List<Point>>();
	
		// Construct an empty visibility set
//		public function new() {
//			segments = new DLL<Segment>();
//			endpoints = new DLL<EndPoint>();
//			open = new DLL<Segment>();
//			center = {x: 0.0, y: 0.0};
//			output = new Array();
//			demo_intersectionsDetected = [];
//		}
	
		Vector3 sPos = Vector3.zero;
			
		public void Flush(){
			segments.Clear();//segments.clear();
			endpoints.Clear();//endpoints.clear();
		}
	
		//initiated from Geometric Visibility
		Vector3 off;//Drawline viewpoint
		public void InitCalc(Vector3 sPos){
			this.sPos = sPos;	
			
			//List<Block> block = new List<Block>();
			//block.Add(new Block{x = 10F, y = 10F, r = 45F});
		
			if(inclBoundary){loadEdgeOfMap(100, 200);}
		
		//points are given in world coordinates
			//setLightLocation(sPos.x, sPos.z);
			
		//points are relative to source coordinates
			setLightLocation(0F, 0F);	//input comes with the lightsource as origin
			off = sPos;	//Debug.Log(off);
		
			DrawEndpoints(ref endpoints);
			
			//Debug.Log("I"+endpoints.Count);		
			sweep();
		
			//Debug.Log("O"+output.Count);
		
			DrawOutput(ref output);	
		
			//maxDistance = 100f;
			ConvertOutput(ref output);		//create poly
					
			Debug.DrawLine(new Vector3(-3F,0F,center.y),new Vector3(+3F,0F,center.y),new Color(1F,1F,1F,0.5F),0F,false);
			Debug.DrawLine(new Vector3(center.x,0F,-3F),new Vector3(center.x,0F,+3F),new Color(1F,1F,1F,0.5F),0F,false);
		}
				
		// Helper function to construct segments along the outside perimeter
		private void loadEdgeOfMap(int size, int margin) {
			//addSegment(margin, margin, margin, size-margin);
			//addSegment(margin, size-margin, size-margin, size-margin);
			//addSegment(size-margin, size-margin, size-margin, margin);
			//addSegment(size-margin, margin, margin, margin);
			
			
			float lenght = 750F;
		
			//Vector3 o = Vector3.zero;//moving borders
			Vector3 o = -sPos;//fixed borders		
		
			addSegment( lenght+o.x,  lenght+o.z,  lenght+o.x, -lenght+o.z);
			addSegment( lenght+o.x, -lenght+o.z, -lenght+o.x, -lenght+o.z);
			addSegment(-lenght+o.x, -lenght+o.z, -lenght+o.x,  lenght+o.z);
			addSegment(-lenght+o.x,  lenght+o.z,  lenght+o.x,  lenght+o.z);
		
			// NOTE: if using the simpler distance function (a.d < b.d)
			// then we need segments to be similarly sized, so the edge of
			// the map needs to be broken up into smaller segments.
		
			if(roundBoundary){
				for(int i = 0; i<rB.Length-1; i++){
					addSegment(rB[i].x, rB[i].y, rB[i+1].x, rB[i+1].y);
				}
				addSegment(rB[rB.Length-1].x, rB[rB.Length-1].y, rB[0].x, rB[0].y);//last Segment
			}
		}
		
		private	Vector2[] rB;
		private	void InitRoundBounds(int resolution){	//res = circle divisions
			float r = roundBRadius;
			float deg = 360F/resolution * Mathf.Deg2Rad;
			rB = new Vector2[resolution];
			for(int i = 0; i<resolution; i++){				
				rB[i]= new Vector2(Mathf.Sin(deg*i)*r, Mathf.Cos(deg*i)*r);
			}
		}
	
		
		// Load a set of square blocks, plus any other line segments
		public void loadMap(int size, int margin, List<Block> blocks, List<Segment> walls) {
			//segments.Clear();//segments.clear();
			//endpoints.Clear();//endpoints.clear();
			loadEdgeOfMap(size, margin);
			
			foreach (Block block in blocks) {
				float x = block.x;
				float y = block.y;
				float r = block.r;
				addSegment(x-r, y-r, x-r, y+r);
				addSegment(x-r, y+r, x+r, y+r);
				addSegment(x+r, y+r, x+r, y-r);
				addSegment(x+r, y-r, x-r, y-r);
			}
			foreach (Segment wall in walls) {
				addSegment(wall.p1.x, wall.p1.y, wall.p2.x, wall.p2.y);
			}
		}
	
	
		// Add a segment, where the first point shows up in the
		// visualization but the second one does not. (Every endpoint is
		// part of two segments, but we want to only show them once.)
		public void addSegment(float x1, float y1, float x2, float y2) {
			Segment segment = new Segment();//null;
			//EndPoint p1 = {begin, x, y, angle,segment, visualize};
		
			//EndPoint p1 = new EndPoint.Init(begin = false, x = 0F, y= 0F, angle = 0F,segment = segment, visualize = true);
			//EndPoint p2 = new EndPoint.Init(begin = false, x = 0F, y= 0F, angle = 0F,segment = segment, visualize = false);
			
			EndPoint p1 = new EndPoint{begin = false, x = 0F, y = 0F, angle = 0F,segment = segment, visualize = true};
			EndPoint p2 = new EndPoint{begin = false, x = 0F, y = 0F, angle = 0F,segment = segment, visualize = false};
			//EndPoint p2 = {begin: false, x: 0.0, y: 0.0, angle: 0.0,segment: segment, visualize: false};
			//segment = {p1: p1, p2: p2, d: 0.0};
			p1.x = x1; p1.y = y1;
			p2.x = x2; p2.y = y2;
			p1.segment = segment;
			p2.segment = segment;
			segment.p1 = p1;
			segment.p2 = p2;
		
			segments.Add(segment);	//segments.append(segment);
			endpoints.Add(p1);	//endpoints.append(p1);
			endpoints.Add(p2);	//endpoints.append(p2);
		
			//Drawline lags one frame behind because off is updated after, this is no bug source
			//Debug.DrawLine(new Vector3(p1.x,0F,p1.y)+off,new Vector3(p2.x,0F,p2.y)+off,new Color(1F,1F,1F,0.5F),0F,false);
		}
	
	
		// Set the light location. Segment and EndPoint data can't be
		// processed until the light location is known.
		public void setLightLocation(float x, float y) {
			center = new Point{x = x, y = y};
			//center.x = x;
			//center.y = y;
			
			foreach (Segment segment in segments) {
				float dx = 0.5F * (segment.p1.x + segment.p2.x) - x;
				float dy = 0.5F * (segment.p1.y + segment.p2.y) - y;
				// NOTE: we only use this for comparison so we can use
				// distance squared instead of distance
				segment.d = dx*dx + dy*dy;
	
				// NOTE: future optimization: we could record the quadrant
				// and the y/x or x/y ratio, and sort by (quadrant,
				// ratio), instead of calling atan2. See
				// <https://github.com/mikolalysenko/compare-slope> for a
				// library that does this.
				segment.p1.angle = Mathf.Atan2(segment.p1.y - y, segment.p1.x - x);
				segment.p2.angle = Mathf.Atan2(segment.p2.y - y, segment.p2.x - x);
	
				float dAngle = segment.p2.angle - segment.p1.angle;
				if (dAngle <= -pi) { dAngle += pi2; }
				if (dAngle > pi) { dAngle -= pi2; }
				segment.p1.begin = (dAngle > 0.0);
				segment.p2.begin = !segment.p1.begin;
			}
		}
	
		// Helper: comparison function for sorting points by angle
		static private int _endpoint_compare(EndPoint a, EndPoint b) {
			// Traverse in angle order
			if (a.angle > b.angle) return 1;
			if (a.angle < b.angle) return -1;
			// But for ties (common), we want Begin nodes before End nodes
			if (!a.begin && b.begin) return 1;
			if (a.begin && !b.begin) return -1;
			return 0;
		}
	
	
		//NO INLINE in C# afaik, manually inline function or trust compiler to inline it
	
		// Helper: leftOf(segment, point) returns true if point is
		// "left" of segment treated as a vector
		static /*inline*/ private bool leftOf(Segment s, Point p){
			var cross = (s.p2.x - s.p1.x) * (p.y - s.p1.y)
					  - (s.p2.y - s.p1.y) * (p.x - s.p1.x);
			return cross < 0;
		}
	
		// Return p*(1-f) + q*f
		static private Point interpolate(EndPoint p, EndPoint q, float f){
			return new Point{x = p.x*(1-f) + q.x*f, y = p.y*(1-f) + q.y*f};
		}
		
		// Helper: do we know that segment a is in front of b?
		// Implementation not anti-symmetric (that is to say,
		// _segment_in_front_of(a, b) != (!_segment_in_front_of(b, a)).
		// Also note that it only has to work in a restricted set of cases
		// in the visibility algorithm; I don't think it handles all
		// cases. See http://www.redblobgames.com/articles/visibility/segment-sorting.html
		private bool intersectionsOccured = false;
		private float shrt = 0.01F; //shortening distance
		private bool _segment_in_front_of(ref Segment a, ref Segment b, Point relativeTo) {	//added ref to segment a to modify it on intersection
			// NOTE: we slightly shorten the segments so that
			// intersections of the endpoints (common) don't count as
			// intersections in this algorithm
			bool A1 = leftOf(a, interpolate(b.p1, b.p2, shrt));
			bool A2 = leftOf(a, interpolate(b.p2, b.p1, shrt));
			bool A3 = leftOf(a, relativeTo);
			bool B1 = leftOf(b, interpolate(a.p1, a.p2, shrt));
			bool B2 = leftOf(b, interpolate(a.p2, a.p1, shrt));
			bool B3 = leftOf(b, relativeTo);
	
			// NOTE: this algorithm is probably worthy of a short article
			// but for now, draw it on paper to see how it works. Consider
			// the line A1-A2. If both B1 and B2 are on one side and
			// relativeTo is on the other side, then A is in between the
			// viewer and B. We can do the same with B1-B2: if A1 and A2
			// are on one side, and relativeTo is on the other side, then
			// B is in between the viewer and A.
			if (B1 == B2 && B2 != B3) return true;
			if (A1 == A2 && A2 == A3) return true;
			if (A1 == A2 && A2 != A3) return false;
			if (B1 == B2 && B2 == B3) return false;
					
			// If A1 != A2 and B1 != B2 then we have an intersection.
			// Expose it for the GUI to show a message. A more robust
			// implementation would split segments at intersections so
			// that part of the segment is in front and part is behind.
			//demo_intersectionsDetected.push([a.p1, a.p2, b.p1, b.p2]);
		
		
			intersectionsOccured = true;
						
			//show intersecting Segment
			Vector3 a1 = new Vector3(a.p1.x, 0F, a.p1.y)+off;
			Vector3 a2 = new Vector3(a.p2.x, 0F, a.p2.y)+off;
			Vector3 b1 = new Vector3(b.p1.x, 0F, b.p1.y)+off;
			Vector3 b2 = new Vector3(b.p2.x, 0F, b.p2.y)+off;
			Debug.DrawLine(a1,a2,Color.magenta,0F,false);
			Debug.DrawLine(b1,b2,Color.cyan,0F,false);
		
			float l = 0.4F;
			Debug.DrawLine(new Vector3(a1.x-l,0F,a1.z),new Vector3(a1.x+l,0F,a1.z),new Color(1F,0F,1F,0.5F),0F,false);
			Debug.DrawLine(new Vector3(a1.x,0F,a1.z-l),new Vector3(a1.x,0F,a1.z+l),new Color(1F,0F,1F,0.5F),0F,false);
			Debug.DrawLine(new Vector3(a2.x-l,0F,a2.z),new Vector3(a2.x+l,0F,a2.z),new Color(1F,0F,1F,0.5F),0F,false);
			Debug.DrawLine(new Vector3(a2.x,0F,a2.z-l),new Vector3(a2.x,0F,a2.z+l),new Color(1F,0F,1F,0.5F),0F,false);
		
		
			Debug.DrawLine(new Vector3(b1.x-l,0F,b1.z-l),new Vector3(b1.x+l,0F,b1.z+l),new Color(0F,1F,1F,0.5F),0F,false);
			Debug.DrawLine(new Vector3(b1.x+l,0F,b1.z-l),new Vector3(b1.x-l,0F,b1.z+l),new Color(0F,1F,1F,0.5F),0F,false);
			Debug.DrawLine(new Vector3(b2.x-l,0F,b2.z-l),new Vector3(b2.x+l,0F,b2.z+l),new Color(0F,1F,1F,0.5F),0F,false);
			Debug.DrawLine(new Vector3(b2.x+l,0F,b2.z-l),new Vector3(b2.x-l,0F,b2.z+l),new Color(0F,1F,1F,0.5F),0F,false);
		
			
			Point intersection = lineIntersection(	new Point{x = a.p1.x, y = a.p1.y},new Point{x = a.p2.x, y = a.p2.y},
													new Point{x = b.p1.x, y = b.p1.y},new Point{x = b.p2.x, y = b.p2.y});
		
			
			/*if(RandBool()){
				a.p2.x = intersection.x;
				a.p2.y = intersection.y;
		
				b.p1.x = intersection.x;
				b.p1.y = intersection.y;
			}else if(RandBool()){
				a.p2.x = intersection.x;
				a.p2.y = intersection.y;
		
				b.p1.x = intersection.x;
				b.p1.y = intersection.y;
			}else if(RandBool()){
				a.p2.x = intersection.x;
				a.p2.y = intersection.y;
		
				b.p2.x = intersection.x;
				b.p2.y = intersection.y;
			}else{
				a.p1.x = intersection.x;
				a.p1.y = intersection.y;
		
				b.p1.x = intersection.x;
				b.p1.y = intersection.y;
			}*/
		
			Debug.DrawLine(new Vector3(intersection.x-l,0F,intersection.y)+off,new Vector3(intersection.x+l,0F,intersection.y)+off,new Color(1F,1F,1F,0.5F),0F,false);
			Debug.DrawLine(new Vector3(intersection.x,0F,intersection.y-l)+off,new Vector3(intersection.x,0F,intersection.y+l)+off,new Color(1F,1F,1F,0.5F),0F,false);
					
			//Debug.Log("Intersection! " +Time.time);
			return false;
	
			// NOTE: previous implementation was a.d < b.d. That's simpler
			// but trouble when the segments are of dissimilar sizes. If
			// you're on a grid and the segments are similarly sized, then
			// using distance will be a simpler and faster implementation.
		}
		
	
		// Run the algorithm, sweeping over all or part of the circle to find
		// the visible area, represented as a set of triangles
		
		//i dont understand it completely but it this sweep
		//	-sorts the segments by angles then loops through them in order and
		//	-recognizes starts of segments and checks if the following segments are in front of the latter
		//	-this is a kind of cheap raycast through the endpoints if you want
	
		int nodeIndex = 0;
		public void sweep() {
			//float maxAngle = 999F;
			intersectionsOccured = false;
			output = new List<Point>();
			endpoints.Sort((ep1, ep2) => (_endpoint_compare(ep1,ep2)));

			open.Clear();
			float beginAngle = 0F;
	
			for (int pass = 0; pass < 2; pass++) {
				foreach (EndPoint p in endpoints) {
					/*if (pass == 1 && p.angle > maxAngle) {
						// Early exit for the visualization to show the sweep process
						break;
					}*/
					
					//var current_old = open.isEmpty()? null : open.head.val;
					Segment current_old = open.Count == 0? null : open[0]; //(if) ? then : else|||head = first (first added), tail last (last added)
					
					if (p.begin) {	//begin is a Endpoint var					
						nodeIndex = 0;
						Segment node = open.Count == 0? null : open[0];		//at the beginning open is empty
							//nodeIndex++;				
						for(;nodeIndex < open.Count-1 && _segment_in_front_of(ref p.segment, ref node, center);) {
							nodeIndex++;
							node = open[nodeIndex];
						}
															
						if (nodeIndex == open.Count) {			//segment is in font of all items in open or first
							open.Add(p.segment);
						}else{
							open.Insert(nodeIndex, p.segment);	//insert before the segment in open that was in front of it and abortedthe for loop
						}					
					}else{
						open.Remove(p.segment);
					}
					
					Segment current_new = open.Count == 0? null : open[0];
					if (current_old != current_new) {
						if (pass == 1) {
							addTriangle(beginAngle, p.angle, current_old);
						}
						beginAngle = p.angle;
					}
				}
			}
		}
	
	
	
		public Point lineIntersection(Point p1, Point p2, Point p3, Point p4){
			// From http://paulbourke.net/geometry/lineline2d/
			var s = ((p4.x - p3.x) * (p1.y - p3.y) - (p4.y - p3.y) * (p1.x - p3.x))
				/ ((p4.y - p3.y) * (p2.x - p1.x) - (p4.x - p3.x) * (p2.y - p1.y));
			return new Point{x = p1.x + s * (p2.x - p1.x), y = p1.y + s * (p2.y - p1.y)};
		}
		
		private void addTriangle(float angle1, float angle2, Segment segment) {
			Point p1 = center;
			Point p2 = new Point{x= center.x + Mathf.Cos(angle1), y=center.y + Mathf.Sin(angle1)};
			Point p3 = new Point{x=0F, y=0F};
			Point p4 = new Point{x=0F, y=0F};
	
			if (segment != null) {
				// Stop the triangle at the intersecting segment
				p3.x = segment.p1.x;
				p3.y = segment.p1.y;
				p4.x = segment.p2.x;
				p4.y = segment.p2.y;
			} else {
				// Stop the triangle at a fixed distance; this probably is
				// not what we want, but it never gets used in the demo
				p3.x = center.x + Mathf.Cos(angle1) * perimeter;
				p3.y = center.y + Mathf.Sin(angle1) * perimeter;
				p4.x = center.x + Mathf.Cos(angle2) * perimeter;
				p4.y = center.y + Mathf.Sin(angle2) * perimeter;
			}
		
			var pBegin = lineIntersection(p3, p4, p1, p2);
	
			p2.x = center.x + Mathf.Cos(angle2);
			p2.y = center.y + Mathf.Sin(angle2);
			var pEnd = lineIntersection(p3, p4, p1, p2);
	
			output.Add(pBegin);//output.push(pBegin);
			output.Add(pEnd);//output.push(pEnd);
		}
	
		private	void DrawEndpoints(ref List<EndPoint> endpoints){
			//Debug.Log(endpoints.Count);
			for(int iP = 0; iP < endpoints.Count-1; iP+=2){
				Vector3	v0 = new Vector3(endpoints[iP].x, 0F, endpoints[iP].y)		+ off;
				Vector3	v1 = new Vector3(endpoints[iP+1].x, 0F, endpoints[iP+1].y)	+ off;
				Debug.DrawLine(v0, v1, new Color(1F,0F,0F,0.5F),0F,false);
			}
		}
	
		private	void DrawOpen(ref List<Segment> open){
			//Debug.Log("OPEN:"+open.Count);
			for(int iP = 0; iP < open.Count-1; iP++){
				Vector3	v0 = new Vector3(open[iP].p1.x, 0F, open[iP].p1.y)		+ off;
				Vector3	v1 = new Vector3(open[iP].p2.x, 0F, open[iP].p2.y)		+ off;
				Debug.DrawLine(v0, v1, Color.yellow,0F,false);
			}
		}
	
		//Color[] colors = new Color[]{Color.yellow,Color.blue,Color.green};
		private	void DrawOutput(ref List<Point> output){
			//Debug.Log(output.Count);
			Vector3 off = sPos;
			for(int iP = 0; iP < output.Count-1; iP+=2){
				Vector3	v0 = new Vector3(output[iP].x, 0F, output[iP].y)		+ off;
				Vector3	v1 = new Vector3(output[iP+1].x, 0F, output[iP+1].y)	+ off;
				//Debug.DrawLine(v0, v1, colors[iP%3],0F,false);
				Debug.DrawLine(v0, v1, new Color(1F,1F,0F,0.5F),0F,false);
			}
		}
	
	
		//convert output to pass it back to GeometricVisibility.cs
		private	void ConvertOutput(ref List<Point> output){
			List<Vector3> outputV3 = new List<Vector3>();
			//we get the points in absolute coordinate system
			Vector3 off = sPos;//source as origin
			//Vector3 off = Vector3.zero;//zero as origin
			for(int iP = 0; iP < output.Count-1; iP+=2){
				outputV3.Add( new Vector3(output[iP].x, 0F, output[iP].y)		+ off);
				outputV3.Add( new Vector3(output[iP+1].x, 0F, output[iP+1].y)	+ off);
			}
			
			GetComponent<GeometricVisibility>().CreateVisibilityPolygon(ref outputV3);
		}
	
//	}
	#endregion
			
	#region VISIBILITY MESH
	//	here we use the output to create a Mesh according to the Visibility:
	//		-2D Visibility Polygon or its inverse
	//		-3D Prism of both
	
	//visibility GameObject
	private	GameObject		visGO;
	private	Mesh			visMesh;			
	private	MeshRenderer	visMeshR;
	private	bool			firstEnter = true;
	public	Material		visMat;
	
	//private	GameObject		copy;
	
	private List<Vector3> postRenderOutput;
	
	public	void CreateVisibilityPolygon(ref List<Vector3> output){
		//the above output consists out of points with source as origin of the coordinate values
		
		//Extruding:
		//if extruding the mesh, the sideplane normals aren't correctly set for performance reasons (should be pointing away/onto to the lightsource direction)
		//bottom could be scrapped for both since it is not visible in most use cases, but we need half of the vertices for the side planes anyway
		//outer side planes are not needed since they are never inside the viewport
		
		//Note:
		//i am not experienced with shaders, so i just set uv/normal/tangents to some default values, if you know better edit it to you liking
		
		if(output.Count == 0){return;}
		
		if(postRender){postRenderOutput = output;}		

		Vector3 pos = source.position;
								
		//build a Mesh on first enter
		if(firstEnter){
			visGO		= new GameObject();
			visMesh		= visGO.AddComponent<MeshFilter>().mesh;
			visMeshR	= visGO.AddComponent<MeshRenderer>();
		
			visGO.transform.position = Vector3.zero;//pos;	//not important
			visGO.transform.name = "VisibilityPolygon";
			//visGO.tag = "Wall";
		
			visMeshR.material = visMat;
			firstEnter = false;
		}
		
//STARSHAPED-VISIBILITY-POLY
		if(!invert){						
			if(!extrude){//NOT EXTRUDED (plane)
				
				int vertCount = output.Count+1;	//+center point
								
				//Create Mesh
				visMesh.Clear();
				Vector3[]	vertices	= new Vector3	[vertCount];
				Color[]		colors		= new Color		[vertCount];
				Vector2[]	uv			= new Vector2	[vertCount];
				Vector3[]	normals		= new Vector3	[vertCount];
				int[]		triangles	= new int		[vertCount*3];
				Vector4[]	tangents	= new Vector4	[vertCount];
										
				//generation of the BottomPlane with Mesh
				vertices[vertCount-1] = pos; //center is stored at the end
				int iT = 0;	//triangle Iterator
				for(int i = 0; i<vertCount-1; i++){	//dont iterate through center		
					vertices[i]	= output[i];
					colors[i]	= Color.white;
					uv[i]		= new Vector2(0F,0F);
					normals[i]	= Vector3.down;	//only down normals are gathered
					tangents[i]	= Vector4.zero;
										
					//triangles: non modulo version
					if(i>0){
						triangles[iT] = vertCount-1;	iT++;	//middle
						triangles[iT] = i;				iT++;	//current
						triangles[iT] = i-1;			iT++;	//previous
					}else{
						triangles[iT] = vertCount-1;	iT++;	//middle
						triangles[iT] = 0;				iT++;	//current
						triangles[iT] = vertCount-2;	iT++;	//previous
					}
				}
				visMesh.vertices = vertices;
				visMesh.colors = colors;
				visMesh.uv = uv;
				//visMesh.uv1 = uv;
				//visMesh.uv2 = uv;
				visMesh.tangents = tangents;
				visMesh.normals = normals;
				visMesh.triangles = triangles;
				//visMesh.RecalculateNormals();
				//visMesh.RecalculateBounds();
				
				
			}else{//EXTRUDED (prism)
														
				//Create Mesh	
				
				int vertCount = output.Count+2;	//+center point + center point on top
				
				Vector3[]	vertices	= new Vector3	[vertCount*2];
				Color[]		colors		= new Color		[vertCount*2];
				Vector2[]	uv			= new Vector2	[vertCount*2];
				Vector3[]	normals		= new Vector3	[vertCount*2];
				Vector4[]	tangents	= new Vector4	[vertCount*2];
				
				int			tOff		= 				vertCount*3;		//	1/4 of the triangles count (1/4 bottom, 1/4 top, 1/2 outer side planes)
				int[]		triangles	= new int		[vertCount*3 *4];	//	*4 because off side Quads and Top triangles	
			
				
				//generate the mesh
				
				Vector3 up = new Vector3(0F,exHeight,0F);
				//center points are stored at the end
				vertices[vertCount-1] = pos;
				vertices[vertCount-2] = pos + up;	
				int iT = 0;
				for(int i = 0; i<vertCount-2; i++){//-2: stop before center points
					
					//just add the original
					vertices[i]	= output[i];
					
					//add additional vertices above the plane-vertices
					vertices[i+vertCount] = output[i] + up;
					
					colors	[i]	= colors[i+vertCount]	= Color.white;
					uv		[i]	= uv[i+vertCount]		= Vector2.zero;
					normals	[i]	= Vector3.zero;
					normals	[i+vertCount] = Vector3.zero;
					tangents[i]	= Vector4.zero;
					
					//mesh triangles per iteration:			
					//
					//	   F█
					//	   / \
					//	  /	  \		TOP
					//	 /	   \
					// C/		\D
					//	█───────█
					//	│\		│	OUTERPLANE1
					//	│ \		│
					//	│  \	│
					//	│	\	│
					//	│	 \	│
					//	│	  \	│
					//	│	   \│	OUTERPLANE2
					//	█───────█
					// A\		/B
					//	 \	   /
					//	  \	  /		BOTTOM
					//	   \ /
					//	   G█
					//
					//	A: previous
					//	B: current
					//	C: previous	+vertCount	(current extended)
					//	D: current	+vertCount	(next extended)
					//	G: centerpoint bottom	(vertCount-1)
					//	F: centerpoint top		(vertCount-2)
					//
					//	triangle 1: GAB	(bottom)
					//	triangle 2: ACB (out2)
					//	triangle 3: BCD (out1)
					//	triangle 4: DCF (top)
					
					//triangles: non modulo version
					if(i>0){
						//middle
						triangles[iT		] = vertCount-1;	//bottom	G
						triangles[iT +tOff*2] = i-1;			//outer2	A
						triangles[iT +tOff*3] = i;				//outer1	B					
						triangles[iT +tOff	] = i+vertCount;	//top		D
						iT++;
						
						//current
						triangles[iT		] = i-1;			//bottom	A
						triangles[iT +tOff*2] = i-1+vertCount;	//outer2	C
						triangles[iT +tOff*3] = i-1+vertCount;	//outer1	C
						triangles[iT +tOff	] = i-1+vertCount;	//top		C
						iT++;
						
						//previous
						triangles[iT		] = i;				//bottom	B
						triangles[iT +tOff*2] = i;				//outer2	B
						triangles[iT +tOff*3] = i+vertCount;	//outer1	D
						triangles[iT +tOff	] = vertCount-2;	//top		F
						iT++;
					}else{//in first iteration i-1 would be out of bounds
						//middle
						triangles[iT		] = vertCount-1;			//bottom	G
						triangles[iT +tOff*2] = vertCount-3;			//outer2	A
						triangles[iT +tOff*3] = i;						//outer1	B					
						triangles[iT +tOff	] = i+vertCount;			//top		D
						iT++;
						
						//current
						triangles[iT		] = vertCount-3;			//bottom	A
						triangles[iT +tOff*2] = vertCount-3+vertCount;	//outer2	C
						triangles[iT +tOff*3] = vertCount-3+vertCount;	//outer1	C
						triangles[iT +tOff	] = vertCount-3+vertCount;	//top		C
						iT++;
						
						//previous
						triangles[iT		] = i;						//bottom	B
						triangles[iT +tOff*2] = i;						//outer2	B
						triangles[iT +tOff*3] = i+vertCount;			//outer1	D
						triangles[iT +tOff	] = vertCount-2;			//top		F
						iT++;
					}
					//Note:	adjacent triangles may be only for side plane generation and top and bottom triangles are just a line
					//		this is why we do i++ rather then i+=2
				}
				
				/*for(int i=3; i<(tOff*4); i+=tOff){
					Debug.DrawLine(vertices[triangles[i  ]], vertices[triangles[i+1]], Color.red, 0F, false);
					Debug.DrawLine(vertices[triangles[i+1]], vertices[triangles[i+2]], Color.red, 0F, false);
					Debug.DrawLine(vertices[triangles[i+2]], vertices[triangles[i  ]], Color.red, 0F, false);
				}*/
													
				visMesh.Clear();
				visMesh.vertices = vertices;
				visMesh.colors = colors;
				visMesh.uv = uv;
				//visMesh.uv1 = uv;
				//visMesh.uv2 = uv;
				visMesh.tangents = tangents;
				visMesh.normals = normals;
				visMesh.triangles = triangles;
				
			
				//visMesh.RecalculateNormals();
				//visMesh.RecalculateBounds();	
			}
				
				
			
//INVERTED-STARSHAPED-VISIBILITY-POLY
		}else{
			//if some output points are farther away than the specified max radius (perimeter) the mesh will get
			//inverted on the outside, but in my use case this portion should not be within the visible viewport anyway
			if(!extrude){//NOT EXTRUDED (plane)
				int vertCount = output.Count;
				//inverted:
				//-Quad behind every Visibility segmentLine			
				
				//Create Mesh
				visMesh.Clear();
				
				//visGO.transform.position = pos;
				
				Vector3[]	vertices	= new Vector3	[vertCount*2];
				Color[]		colors		= new Color		[vertCount*2];
				Vector2[]	uv			= new Vector2	[vertCount*2];
				Vector3[]	normals		= new Vector3	[vertCount*2];
				int[]		triangles	= new int		[vertCount*6];
	
			//generation of the BottomPlane with Mesh
				int iT = 0;	//triangle Iterator
				int tO = vertCount * 3;	//triangle offset
				for(int i = 0; i<vertCount; i++){
				//for(int i = 1; i<vertCount; i++){
					
					vertices[i]				= output[i];
					vertices[i+vertCount]	= source.position + (vertices[i]-source.position).normalized * perimeter;
						//multilicator should be great enough to surpass the viewport edges
					
					
					//Debug.DrawLine(source.position+vertices[i+vertCount],source.position,Color.blue,0F,false);
					
					//Debug.DrawRay(source.position, vertices[i+vertCount], Color.blue, 0F, false);
					//Debug.DrawLine(vertices[i],source.position,Color.blue,0F,false);
					//Debug.DrawLine(vertices[i],vertices[i+vertCount],Color.green,0F,false);			
					
					colors[i]	=	colors[i+vertCount]	= Color.white;
					uv[i]		=	uv[i+vertCount]		= Vector2.zero;
					normals[i]	=	normals[i+vertCount]= Vector3.down;	//only down normals are gathered
					
					// C		 D
					//	█───────█
					//	│\		│
					//	│ \		│
					//	│  \	│
					//	│	\	│
					//	│	 \	│
					//	│	  \	│
					//	│	   \│
					//	█───────█
					// A		 B
					//
					//	A: current
					//	B: next
					//	C: current	+vertCount	(current extended)
					//	D: next		+vertCount	(next extended)
					
					//	triangle 1: ABC
					//	triangle 2: BDC
					
					//triangles: modulo version
					/*triangles[iT]		= i;							//A
					triangles[iT+tO]	= (i+1)%(vertCount);			//B
					iT++;
					triangles[iT]		= (i+1)%(vertCount);			//B
					triangles[iT+tO]	= (i+1)%(vertCount)+vertCount;	//D
					iT++;
					triangles[iT]		= i+vertCount;					//C
					triangles[iT+tO] 	= i+vertCount;					//C
					iT++;*/				
					
					//triangles: non modulo version
					if(i>0){
						triangles[iT]		= i-1;				//A
						triangles[iT+tO]	= i;				//B
						iT++;
						triangles[iT]		= i;				//B
						triangles[iT+tO]	= i+vertCount;		//D
						iT++;
						triangles[iT]		= (i-1)+vertCount;	//C
						triangles[iT+tO] 	= (i-1)+vertCount;	//C
						iT++;
					}else{
						triangles[iT]		= vertCount-1;		//A
						triangles[iT+tO]	= 0;				//B
						iT++;
						triangles[iT]		= 0;				//B
						triangles[iT+tO]	= 0+vertCount;		//D
						iT++;
						triangles[iT]		= vertCount-1;		//C
						triangles[iT+tO] 	= vertCount-1;		//C
						iT++;
					}
				}	
				
				visMesh.vertices = vertices;
				visMesh.colors = colors;
				visMesh.uv = uv;
				//visMesh.uv1 = uv;
				//visMesh.uv2 = uv;
				//visMesh.tangents
				visMesh.normals = normals;
				visMesh.triangles = triangles;
				
				
			}else{//EXTRUDED (prism)
											
				//Create Mesh					
				int vertCount = output.Count;				
				Vector3[]	vertices	= new Vector3	[vertCount*4];
				Color[]		colors		= new Color		[vertCount*4];
				Vector2[]	uv			= new Vector2	[vertCount*4];
				Vector3[]	normals		= new Vector3	[vertCount*4];
				
				int			tOff		= 				vertCount*3;		//	1/4 of the triangles count (1/4 bottom, 1/4 top, 1/4 inner side planes, 1/2 outer side planes)
				int[]		triangles	= new int		[vertCount*3 *8];	//	*8 because off side Quads (4 triangles/output point) and Top/bottom Quads (another 4)	
			
				
				//generate the mesh				
				Vector3 up = new Vector3(0F,exHeight,0F);
				//center points are stored at the end	
				int iT = 0;
				int tO = tOff;			//triangle offset
				int eO = vertCount;		//edge offset
				for(int i = 0; i<vertCount; i++){//-2: stop before center points
										
					vertices[i]			= output[i];		//original output vertex
					
					vertices[i +eO]		= output[i] +up;	//above original
					
					//original vertex projected away from the lightsource onto the outer perimeter:
					vertices[i +eO*2] 	= ( source.position + (vertices[i]-source.position).normalized * perimeter );
					
					vertices[i +eO*3]	= vertices[i +eO*2] +up;	//above extended
					
										
					colors	[i]	= colors[i+vertCount]	= Color.white;
					uv		[i]	= uv[i+vertCount]		= new Vector2(0F,0F);
					normals	[i]	= Vector3.down;
					normals	[i+vertCount] = Vector3.up;
					
					//mesh triangles per iteration:			
					//
					// cur.iteration -> next iteration, scrap top bottom every 2nd cycle in future..
					//
					// A	 B
					//	█───█			────█B+1
					//	│\	│	inner2		│
					//	│ \ │				│
					//	│  \│	inner1		│
					// G█───█H			────█H+1
					//	│\	│	top2
					//	│ \	│
					//	│  \│	top1
					// E█───█F			────█E+1
					//	│\	│	outer2		│
					//	│ \	│				│
					//	│  \│	outer1		│
					// C█───█D			────█D+1
					//	│\	│	Bottom2
					//	│ \	│
					//	│  \│	Bottom1
					//	█───█
					// A	 B
					//
					//	Bottom vertices
					//	A: i-1			previous
					//	B: i			current
					//	C: i-1	+eO*2	previous extended
					//	D: i	+eO*2	current extended
					//
					//	Top vertices
					//	E: i-1	+eO*3	previous top extended
					//	F: i	+eO*3	current top extended
					//	G: i-1	+eO		previous top
					//	H: i	+eO		current top
					//
					//	triangle 1: ACB	(bottom1)
					//	triangle 2: BCD	(bottom2)
					//	triangle 3: CED	(outer1)
					//	triangle 4: DEF	(outer2)
					//	triangle 5: EGF	(top1)
					//	triangle 6: FGH	(top2)
					//	triangle 7: GAH	(inner1)
					//	triangle 8: HAB	(inner2)
					
					//triangles: non modulo version
					if(i>0){
						//1st vertex
						triangles[iT	  ] = i-1;			//bottom1	A
						triangles[iT +tO  ] = i;			//bottom2	B
						triangles[iT +tO*2] = i-1	+eO*2;	//outer1	C
						triangles[iT +tO*3] = i		+eO*2;	//outer2	D
						triangles[iT +tO*4] = i-1	+eO*3;	//top1		E
						triangles[iT +tO*5] = i		+eO*3;	//top2		F
						triangles[iT +tO*6] = i-1	+eO;	//inner1	G
						triangles[iT +tO*7] = i		+eO;	//inner2	H
						iT++;
												
						//2nd vertex
						triangles[iT	  ] = i-1	+eO*2;	//bottom1	C
						triangles[iT +tO  ] = i-1	+eO*2;	//bottom2	C
						triangles[iT +tO*2] = i-1	+eO*3;	//outer1	E
						triangles[iT +tO*3] = i-1	+eO*3;	//outer2	E
						triangles[iT +tO*4] = i-1	+eO;	//top1		G
						triangles[iT +tO*5] = i-1	+eO;	//top2		G
						triangles[iT +tO*6] = i-1;			//inner1	A
						triangles[iT +tO*7] = i-1;			//inner2	A
						iT++;
						
						//3rd vertex						
						triangles[iT	  ] = i;			//bottom1	B
						triangles[iT +tO  ] = i		+eO*2;	//bottom2	D
						triangles[iT +tO*2] = i		+eO*2;	//outer1	D
						triangles[iT +tO*3] = i		+eO*3;	//outer2	F
						triangles[iT +tO*4] = i		+eO*3;	//top1		F
						triangles[iT +tO*5] = i		+eO;	//top2		H
						triangles[iT +tO*6] = i		+eO;	//inner1	H
						triangles[iT +tO*7] = i;			//inner2	B						
						iT++;												
					}else{//in first iteration i-1 would be out of bounds, and modulo (%) is costlier than an if
						//1st vertex
						triangles[iT	  ] = vertCount-1;			//bottom1	A
						triangles[iT +tO  ] = i;					//bottom2	B
						triangles[iT +tO*2] = vertCount-1	+eO*2;	//outer1	C
						triangles[iT +tO*3] = i				+eO*2;	//outer2	D
						triangles[iT +tO*4] = vertCount-1	+eO*3;	//top1		E
						triangles[iT +tO*5] = i				+eO*3;	//top2		F
						triangles[iT +tO*6] = vertCount-1	+eO;	//inner1	G
						triangles[iT +tO*7] = i				+eO;	//inner2	H
						iT++;
												
						//2nd vertex
						triangles[iT	  ] = vertCount-1	+eO*2;	//bottom1	C
						triangles[iT +tO  ] = vertCount-1	+eO*2;	//bottom2	C
						triangles[iT +tO*2] = vertCount-1	+eO*3;	//outer1	E
						triangles[iT +tO*3] = vertCount-1	+eO*3;	//outer2	E
						triangles[iT +tO*4] = vertCount-1	+eO;	//top1		G
						triangles[iT +tO*5] = vertCount-1	+eO;	//top2		G
						triangles[iT +tO*6] = vertCount-1;			//inner1	A
						triangles[iT +tO*7] = vertCount-1;			//inner2	A
						iT++;
						
						//3rd vertex						
						triangles[iT	  ] = i;					//bottom1	B
						triangles[iT +tO  ] = i				+eO*2;	//bottom2	D
						triangles[iT +tO*2] = i				+eO*2;	//outer1	D
						triangles[iT +tO*3] = i				+eO*3;	//outer2	F
						triangles[iT +tO*4] = i				+eO*3;	//top1		F
						triangles[iT +tO*5] = i				+eO;	//top2		H
						triangles[iT +tO*6] = i				+eO;	//inner1	H
						triangles[iT +tO*7] = i;					//inner2	B						
						iT++;
					}
					//Note:	adjacent triangles may be only for side plane generation and top and bottom triangles are just a line
					//		this is why we do i++ rather then i+=2
					//		otherwise the would be holes in the side planes between points of different distance
					//		but this leads to unused (0 area) top and bottom quads between every segment
				}
				
				/*for(int i=0; i<(tO*8); i+=tO){
					Debug.DrawLine(vertices[triangles[i  ]], vertices[triangles[i+1]], Color.red, 0F, false);
					Debug.DrawLine(vertices[triangles[i+1]], vertices[triangles[i+2]], Color.red, 0F, false);
					Debug.DrawLine(vertices[triangles[i+2]], vertices[triangles[i  ]], Color.red, 0F, false);
				}*/
				
				//Copy original Triangles and add inverted triangles for the top
				//bottom could be scrapped for both since it is not visible in most use cases, but we need half of the vertices for the side planes anyway
				//inner side planes are not needed since they are never inside the viewport
				//outer side planes, they just need the inner, not extended vertices
									
				visMesh.Clear();
				visMesh.vertices = vertices;
				visMesh.colors = colors;
				visMesh.uv = uv;
				//visMesh.uv1 = uv;
				//visMesh.uv2 = uv;
				//visMesh.tangents = new Vector4[0];
				visMesh.normals = normals;
				visMesh.triangles = triangles;
				
				//visMesh.RecalculateNormals();
				//visMesh.RecalculateBounds();
				
			}
		}
		
		
		/*if(copy){Destroy(copy);}
		copy = Instantiate(visGO) as GameObject;
		copy.transform.position += new Vector3(0F,2.5F,0F);*/	
	}
	#endregion
					
	#region	Camera & Fadertest
	//camera panning variables
	private	Vector3 curOrigin;
	private	Vector3 camOrigin;
	private	Vector3 lastPos;
	
	//camera lerp
	private	bool lerpCam = false;
	private	float lerpTime = 0F;	//time lerp started
	private float lerpSpeed = 2F;	//seconds lerp is performed
	private Vector3 targetPosition;	//target position of camera
	
	private	void CameraManipulation(){
		if(!mouseOver){
			RaycastHit hit;
			Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition),out hit);
			Vector3 pos = hit.point;
				if(Input.GetKeyDown("mouse 1")){lastPos = pos;lerpCam = false;}
				if(Input.GetKey("mouse 1")){
					cam.transform.position = (camOrigin + (curOrigin - lastPos));
					//renew point
					Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition),out hit);
					lastPos		= hit.point;
					camOrigin	= cam.transform.position;
				}else{
					camOrigin	= cam.transform.position;
					curOrigin	= pos;
					if(Input.GetKey("mouse 0")){
						source.position = pos;
					}
				}
			source.position = new Vector3(source.position.x,0F,source.position.z);
		}
		
		if(lerpCam){
			cam.transform.position = Vector3.Lerp(cam.transform.position,targetPosition,(Time.time-lerpTime)/lerpSpeed);
			if(Time.time-lerpTime>lerpSpeed){lerpCam = false;}
		}
		
		if (Input.GetAxis("Mouse ScrollWheel") < 0){	// back, zoom out
			if(cam.orthographicSize * 1.25F < 500F){
				cam.orthographicSize *= 1.25F;
			}
		}else if (Input.GetAxis("Mouse ScrollWheel") > 0){	// forward, zoom in
			if(cam.orthographicSize * 1.25F > 12F){
				cam.orthographicSize *= 0.8F;
			}
		}
	}
	
	private	void FaderTest(ref BottomPolygon[] poly){
		for(int ip = 0; ip < poly.Length; ip++){	//polygon
			if(poly[ip].fader){
				poly[ip].fader.Fade(fadeTest);
			}
		}
	}
	
	//
	private void Refresh(){
		walls = GameObject.FindGameObjectsWithTag("Wall");
		GeneratePolygonStructArr(ref staticBlockers);
		RefreshRandPolygons(ref randomPolys);
		FaderTest(ref staticBlockers);
	}
	
	#endregion
				
	#region GUI
	
	private int topOff = 0;		//scrolling through GUI
	private int guisize = 944; //total size of the GUI (buttons on left) in pixels
	private int linecount2ndApp = 0;
	public	string lastTooltip = " ";
	
	//STUFF
	private	bool fadeTest			= true;		
	private	int  polyCount			= 0;	
	private bool cameraTop			= false;
	private	bool mouseOver			= false;	
	private	bool vsyncOn			= false;
	private	int  frameRate			= 0;
	
	private	void OnGUI(){
		
	//this box stops lightsource dragging while operating slider exploiting tooltip (checking if mouse is above this box)
	GUI.Box(new Rect(-100,-100-topOff,300,1500),new GUIContent("","1"));	
		
		//Slider
		if			(Input.mousePosition.y > Screen.height-25 && Input.mousePosition.x < 310){
			topOff-=3;
		}else if	(Input.mousePosition.y < 25 && Input.mousePosition.x < 310){
			topOff+=3;
		}			
		topOff = (int)GUI.VerticalScrollbar(new Rect(5, 15, 15, Screen.height-30), topOff, Screen.height+Screen.height-guisize, 0F, Screen.height-30);
				
		int	bh	= 22;
		int	y	= 10;
		int	x	= 45;
		int x2	= 25;
		int	dy	= 22;
		int dy2	= 10;
		int	bl	= 120;
		int	off = 125;
		GUI.skin.button.fontSize = 11;
		GUI.skin.toggle.fontSize = 11;
		GUI.skin.label .fontSize = 12;
				
	//Controls
		GUI.skin.label .fontSize = 22;
		GUI.Label (new Rect(Screen.width-230,y,230,bh*4),"Controls");
		GUI.skin.label .fontSize = 12;
		GUI.Label (new Rect(Screen.width-225,y+30,250,bh*9),"" +
			"Light Position:\tLeft Mouse\n" +
			"Zoom:\tMouseWheel\n"+
			"Pan:\t\tHold RightMouseButton\n\n"+
			"enable Gizmos if in Editor!\n");
		
		
		GUI.skin.label .fontSize = 16;
		GUI.Label (new Rect(Screen.width-230,y+115,230,bh*4),"FPS: ~"+frameRate);
		GUI.skin.label .fontSize = 12;
				
		y	= y-topOff;
				
	//Visibility Polygon Options
		GUI.Label (new Rect(x2,y,400,bh),"Visibility Polygon Options:"); y+=dy;
		
		if(GUI.Button (new Rect(x,	y, bl, bh),	"Inverted"))	{
			invert = !invert; perimeter= invert?800F:200F;
			if(invert){
				visMeshR.material = darkness;
				if(extrude){cubeParent.SetActive(true);}
			}else{
				visMeshR.material = light;
				cubeParent.SetActive(false);
			}
			invisibleFaces = invert;
		}
		invert	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	invert, ""));	y+=dy;
		
		GUI.Label (new Rect(x,y,400,bh),"Radius:"); y+=15;
		perimeter = GUI.HorizontalSlider(new Rect(x,	y, bl, bh),perimeter,0.1F,1000F);	y+=dy;
		
		if(GUI.Button (new Rect(x,	y, bl, bh),	"InvisibleFaces"))	{invisibleFaces = !invisibleFaces;}
		invisibleFaces	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	invisibleFaces, ""));	y+=dy;
		
		string prismS = "[Plane]"; if(extrude){prismS = "[Prism]";}
		if(GUI.Button (new Rect(x,	y, bl, bh),	"Extrude "+prismS))	{
			extrude = !extrude;
			if(extrude && invert){cubeParent.SetActive(true);}
		}
		extrude	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	extrude, ""));	y+=dy;
		
		if(!extrude){GUI.enabled = false;}
		GUI.Label (new Rect(x,y,400,bh),"extrusion height:"); y+=15;
		exHeight = GUI.HorizontalSlider(new Rect(x,	y, bl, bh),exHeight,0.1F,10F);	y+=dy;
		GUI.enabled = true;
		
	//Gathering Options
		GUI.Label (new Rect(x2,y,400,bh),"Segments Gathering:"); y+=dy;
			
		if(GUI.Button (new Rect(x,	y, bl, bh),	"include Boundary"))	{inclBoundary = !inclBoundary;}
		inclBoundary	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	inclBoundary, ""));	y+=dy;
		
		if(!inclBoundary){GUI.enabled = false;}
		
		if(GUI.Button (new Rect(x,	y, bl, bh),	"↘+round Boundary"))	{roundBoundary = !roundBoundary;}
		roundBoundary	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	roundBoundary, ""));	y+=dy;
		
		GUI.Label (new Rect(x,y,400,bh),"radius from source: "+roundBRadius.ToString("F2")); y+=15;
		roundBRadius = GUI.HorizontalSlider(new Rect(x,	y, bl, bh),roundBRadius,10F,500F);	y+=dy;
		
		if(inclBoundary && roundBRadius != lastRBRadius){
			InitRoundBounds(45);	//90 = 4° segments
		}
		
		GUI.enabled = true;
			
		if(GUI.Button (new Rect(x,	y, bl, bh),	"CheapGather"))	{cheapGather = !cheapGather;}
		cheapGather	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	cheapGather, ""));	y+=dy;
		
		if(!cheapGather){GUI.enabled = false;}
		
		if(GUI.Button (new Rect(x,	y, bl, bh),	"↘+drop if not onScreen"))	{inclPerRenderer = !inclPerRenderer;}
		inclPerRenderer	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	inclPerRenderer, ""));	y+=dy;
		
		if(inclPerRenderer){inclPerRadius = false;}
			
		if(GUI.Button (new Rect(x,	y, bl, bh),	"↘+drop per distance"))	{inclPerRadius = !inclPerRadius;}
		inclPerRadius	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	inclPerRadius, ""));	y+=dy;
		
		if(inclPerRadius){inclPerRenderer = false;}else{GUI.enabled = false;}
		
		GUI.Label (new Rect(x,y,400,bh),"radius from source: "+inclRadius.ToString("F2")); y+=15;
		inclRadius = GUI.HorizontalSlider(new Rect(x,	y, bl, bh),inclRadius,10F,800F);	y+=dy;
		GUI.enabled = true;
		
		
		GUI.enabled = true;
	//DRAW
		GUI.Label (new Rect(x2,y,400,bh),"Draw:"); y+=dy;					//   ↓ needed to check if hovering
		if(GUI.Button (new Rect(x,	y, bl, bh),	"ActiveEdges"))	{drawOutput = !drawOutput;}y+=dy;
		if(GUI.Button (new Rect(x,	y, bl, bh),	"Polygons"))	{drawPolygons = !drawPolygons;}y+=dy;
		GUI.enabled = true;
		
		//indicators
		y-=dy*2;
		GUI.enabled = false;
		drawOutput			= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	drawOutput, ""));			y+=dy;
		drawPolygons		= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	drawPolygons, ""));			y+=dy;
		GUI.enabled = true;
		
		
	
	y+=dy2;	
		
		GUI.Label (new Rect(x2,y,600,bh),"Create Random Polygon ["+polyCount+"]"); y+=dy;
		if(GUI.Button (new Rect(x,	y, bl/2, bh),	"Create"))	{CreateRandomPolygon(ref randomPolys); drawPolygons = true;}
		if(GUI.Button (new Rect(x+bl/2,y, bl/2, bh),	"Clear"))	{ClearRandomPolygons(ref randomPolys);}y+=dy;
		if(GUI.Button (new Rect(x,	y, bl, bh),	"Auto"))	{autoGen = !autoGen; drawPolygons = true;}
		GUI.enabled = false;
		autoGen	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	autoGen, ""));y+=dy;
		GUI.enabled = true;
		y+=10;
		GUI.Label (new Rect(x2,y,600,bh),"Create Random Prism ["+prismcount+"]"); y+=dy;
		if(GUI.Button (new Rect(x,	y, bl, bh),	"CreateRandomPrism"))	{CreateBottomPlane(true); Refresh();}y+=dy;
				
	y+=dy2;		
	//Polygon Extraction/Generation
		GUI.Label (new Rect(x2,y,400,bh),"Polygon Extraction/Generation:"); y+=dy;
		
		if(GUI.Button (new Rect(x,	y, bl, bh),	"Refresh"))	{
			Refresh();
		}
		y+=dy;
		if(GUI.Button (new Rect(x+20,	y, bl-20, bh),	"↘checkCCW"))	{checkCCW = !checkCCW;}
		checkCCW	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	checkCCW, ""));	y+=dy;
		
		if(GUI.Button (new Rect(x+20,	y, bl-20, bh),	"↘worldNormals"))	{worldNormals = !worldNormals;}
		worldNormals	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	worldNormals, ""));	y+=dy;
					
		if(GUI.Button (new Rect(x+20,	y, bl-20, bh),	"↘clampY to 0"))	{clampY = !clampY;}
		clampY	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	clampY, ""));	y+=dy;
				
	y+=dy2;
	//Other Options
		GUI.Label (new Rect(x2,y,400,bh),"Other Stuff:"); y+=dy;
		
		if(GUI.Button (new Rect(x,	y, bl, bh),	"VSync"))	{ToggleVsync();}
		vsyncOn	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	vsyncOn, ""));
		y+=dy;
		
		if(GUI.Button (new Rect(x,	y, bl, bh),	"Top View"))	{cameraTop = !cameraTop;
			ToggleTopView();
		}
		cameraTop	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	cameraTop, ""));	y+=dy;
			
		if(GUI.Button (new Rect(x,	y, bl, bh),	"TestFader"))	{fadeTest = !fadeTest; FaderTest(ref staticBlockers);}
		fadeTest	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	fadeTest, ""));	y+=dy;
								
	y+=dy2;		
			
		//Mouse above GUI check
		if (Event.current.type == EventType.Repaint && GUI.tooltip != lastTooltip) {
			if (lastTooltip != "")
				SendMessage("OnMouseOut", SendMessageOptions.DontRequireReceiver);
			
			if (GUI.tooltip != "")
				SendMessage("OnMouseOver", SendMessageOptions.DontRequireReceiver);
			lastTooltip = GUI.tooltip;
		}
		
		
		if(!intersectionsOccured)return;
		GUI.skin.label .fontSize = 12;
		GUI.Label (new Rect(Screen.width-630,50,500,500),"Intersections Occured\nErrors may appear");
		
	}//↘↓↙←↖↑↗→
	
	private	void ToggleVsync(){
		if(vsyncOn){
			QualitySettings.vSyncCount = 0;
		}else{
			QualitySettings.vSyncCount = 1;
		}
		vsyncOn = !vsyncOn;
	}
	
	private void ToggleTopView(){
		if(cameraTop)	{cam.transform.rotation = Quaternion.Euler(90F,0F,0F);}
		else			{cam.transform.rotation = Quaternion.Euler(60F,0F,0F);}
	}
	
	private	IEnumerator FrameRateUpdate() {
		while(true){
			frameRate = (int)(1F/Time.deltaTime);
			yield return new WaitForSeconds(0.3F);
		}
	}
		
	private	void OnMouseOver()	{ mouseOver	=  true;}
	private	void OnMouseOut()	{ mouseOver	= false;}
	
	#endregion
	
	#region	Utility Functions	
	//RANDOM POLY
	private	void CreateRandomPolygon(ref BottomPolygon[] poly){
		BottomPolygon[] enlargedArray = new BottomPolygon[poly.Length+1];
		//just a quick and dirty distorted circle... = simple polygon, convex or concave possible
		int edgeCount = Random.Range(3,20);
		float radius  = Random.Range(10F,30F);
		Vector3 pos = new Vector3(Random.Range(-220F,220F),0F,Random.Range(-220F,220F));
		List<Vector3> newPoly = new List<Vector3>();
		
		//create some random angles
		List<float> angles = new List<float>();	
		for(int i = 0; i < edgeCount; i++){
			float angle = Random.Range(0F,pi2);
			angles.Add(angle);
		}
		
		//sort Array by either des or asc to test FixCCWOrder
		if(RandBool()){
			angles.Sort((p1, p2) => (p1.CompareTo(p2)));//Debug.Log("normal order");
		}else{
			angles.Sort((p2, p1) => (p1.CompareTo(p2)));//Debug.Log("reversed order");
		}
		
		for(int i = 0; i < edgeCount; i++){
			float dis = Random.Range(0.2F,1F)*radius;	//distance to center
			newPoly.Add(new Vector3(Mathf.Sin(angles[i])*dis,0F,Mathf.Cos(angles[i])*dis) +pos);
		}
		
		//copy old Polys to Polygon Array
		for(int i = 0; i< enlargedArray.Length-1; i++){
			enlargedArray[i].vertices = poly[i].vertices;
		}
		
		//check Order
		if(checkCCW){
			FixCCWOrder(ref newPoly);
		}
		
		//add new poly at last position
		enlargedArray[enlargedArray.Length-1].vertices = newPoly.ToArray();
		
		//save new array
		poly = enlargedArray;
		//CreateBottomPlane(false);
		
		RefreshRandPolygons(ref randomPolys);
	}
	
	private	void ClearRandomPolygons(ref BottomPolygon[] polyArr){
		polyArr = new BottomPolygon[0];
	}
	
	private	void RefreshRandPolygons(ref BottomPolygon[] polyArr){
		//check Order
		if(checkCCW){
			for(int i = 0; i < polyArr.Length; i++){
				polyArr[i].vertices = FixCCWOrder(polyArr[i].vertices);
				polyArr[i].include = true;
				CalculateCenterAndRadius(ref polyArr[i]);
			}
		}
	}
	
	private	bool RandBool(){return (Random.value > 0.5f);}//just a random bool

	
	//RANDOM PRISM
	//creates a Bottomplane with GameObject and MeshRenderer and similar characteristic as the visibility Polygon (all triangles have center point)
	//used to test the prism transformation
	//Prism will be used as shadowObject to get rid of the viewport cutting technique in my game (SkyNox)
	public Material prismMat;
	private int prismcount = 0;
	private	void CreateBottomPlane(bool random){
		
		Vector3 pos = Vector3.zero;
		List<Vector3> newPlane = new List<Vector3>();
		int edgeCount = 0;
		if(random){
			//just a quick and dirty distorted circle... = simple polygon, convex or concave possible
			edgeCount = Random.Range(3,12);
			float radius  = Random.Range(10F,30F);
			pos = new Vector3(Random.Range(-220F,220F),0F,Random.Range(-220F,220F));
			//create some random angles
			List<float> angles = new List<float>();	
		
			for(int i = 0; i < edgeCount; i++){
				//float angle = Random.Range(0F,pi2);
				float angle = pi2/edgeCount*i;
				angles.Add(angle);
			}
			
			//sort Array by either des or asc to test FixCCWOrder
			/*if(RandBool()){
				angles.Sort((p1, p2) => (p1.CompareTo(p2)));//Debug.Log("normal order");
			}else{
				angles.Sort((p2, p1) => (p1.CompareTo(p2)));//Debug.Log("reversed order");
			}*/
			
			newPlane.Add(pos);//add middle point at start
			for(int i = 0; i < edgeCount; i++){
				//float dis = Random.Range(0.2F,1F)*radius;	//distance to center
				float dis = 1F*radius;	//distance to center
				newPlane.Add(new Vector3(Mathf.Sin(angles[i])*dis,0F,Mathf.Cos(angles[i])*dis) +pos);
			}
		}else{
			//entered after random poly has been created and put on last position of List
			BottomPolygon poly = randomPolys[randomPolys.Length-1];
			pos = poly.position;
			edgeCount = poly.vertices.Length;
			newPlane.Add(pos);//add middle point at start
			for(int i = 0; i < edgeCount; i++){
				newPlane.Add(poly.vertices[i]);
			}
		}
		
		
		//build a Mesh
		GameObject newGO = new GameObject();
		Mesh mesh = newGO.AddComponent<MeshFilter>().mesh;
		MeshRenderer meshR = newGO.AddComponent<MeshRenderer>();
		prismcount++;
		newGO.transform.position = pos;
		newGO.transform.name = "Prism"+prismcount;
		newGO.tag = "Wall";
	
		//Create Mesh
		mesh.Clear();
		Vector3[]	vertices = new Vector3[edgeCount+1];
		Color[]		colors = new Color[edgeCount+1];
		Vector2[]	uv = new Vector2[edgeCount+1];
		Vector3[]	normals = new Vector3[edgeCount+1];
		int[]		triangles = new int[(edgeCount)*3];
	
		//Matrix4x4 localSpaceTransform = transform.worldToLocalMatrix;
	
	//generation of the BottomPlane with Mesh
		int iT = 0;	//triangle Iterator
		for(int i = 0; i<edgeCount+1; i++){
			vertices[i]	= newGO.transform.InverseTransformPoint(newPlane[i]);//localSpaceTransform.MultiplyPoint(newPlane[i]);
			colors[i]	= Color.white;
			uv[i]		= new Vector2(0F,0F);
			normals[i]	= Vector3.down;	//only down normals are gathered

			if(i>0){//ignore first point(middle point)
				triangles[iT] = 0;						iT++;	//middle
				triangles[iT] = i;						iT++;	//current Point
				triangles[iT] = (i+1)%(edgeCount+1);	iT++;	//nextPoint
			}
			//last vertex
			if(i== edgeCount){
				//last triangle would add 0 twice
				triangles[iT-1] = 1;
			}
		}
		mesh.vertices = vertices;
		mesh.colors = colors;
		mesh.uv = uv;
		//mesh.uv1 = uv;
		//mesh.uv2 = uv;
		//mesh.tangents
		mesh.normals = normals;
		mesh.triangles = triangles;
		meshR.material = prismMat;				
	//Convert to prism
		if(random)
		ConvertPlaneToPrism(ref newGO);
		//focus new prism
		lerpCam		= true;
		lerpTime	= Time.time;
		targetPosition = pos;
	}


	//it would be much cheaper if we directly generate the plane extruded
	private	void ConvertPlaneToPrism(ref GameObject plane){
									//visibility poly,	origin for Drawline, starpoly or inverse, showtriangles
		
	
		float height = 2.5F;//height of the prism
			
		Mesh mesh = plane.GetComponent<MeshFilter>().mesh;
		MeshRenderer meshR = plane.GetComponent<MeshRenderer>();
		int countVOld = mesh.vertices.Length;
		int countV = countVOld*2;
				
		//Create Mesh
		Vector3[]	vertices = new Vector3[countV];
		Color[]		colors = new Color[countV];
		Vector2[]	uv = new Vector2[countV];
		Vector3[]	normals = new Vector3[countV];
		int[]		triangles = new int[(countV*3)*2];//*2 because off side triangles		
	
		
		//generate the mesh
		for(int i = 0; i<countVOld; i++){
			//just add the original
			vertices[i]				= mesh.vertices[i];
			colors[i]				= mesh.colors[i];
			uv[i]					= mesh.uv[i];
			normals[i]				= mesh.normals[i];
					
			//add an additional above the old
			vertices[i	+countVOld]	= mesh.vertices[i] + Vector3.up*height;
			colors[i	+countVOld]	= mesh.colors[i];
			uv[i		+countVOld]	= mesh.uv[i];
			normals[i	+countVOld]	= Vector3.up;				
		}
		
		//Copy original Triangles and add inverted triangles for the top
		for(int iT = 0; iT < mesh.triangles.Length;){
			//top and bottom plane
			//this inverts the triangles of bottom plane to face down (-1, +1 below)
			triangles[iT]					= mesh.triangles[iT];//Debug.Log(vertices[triangles[iT]]);//has to be 0,0,0
			triangles[iT+(countVOld*3)]		= mesh.triangles[iT]+countVOld; iT++;
			triangles[iT]					= mesh.triangles[iT+1];
			triangles[iT+(countVOld*3)]		= mesh.triangles[iT]+countVOld; iT++;
			triangles[iT]					= mesh.triangles[iT-1];
			triangles[iT+(countVOld*3)]		= mesh.triangles[iT]+countVOld; //iT++;
		
			//side planes, rectangle
			triangles[iT+(countVOld*6)-2]	= triangles[iT];
			triangles[iT+(countVOld*6)-1]	= triangles[iT-1];
			triangles[iT+(countVOld*6)]		= triangles[iT-1+(countVOld*3)];
		
			triangles[iT+(countVOld*9)-2]	= triangles[iT+(countVOld*3)];
			triangles[iT+(countVOld*9)-1]	= triangles[iT-1+(countVOld*3)];
			triangles[iT+(countVOld*9)]		= triangles[iT-1];
			
			iT++;
		}
		
		
		mesh.Clear();
		mesh.vertices = vertices;
		mesh.colors = colors;
		mesh.uv = uv;
		//mesh.uv1 = uv;
		//mesh.uv2 = uv;
		//mesh.tangents
		mesh.normals = normals;
		mesh.triangles = triangles;
		
	
		//mesh.RecalculateNormals();
		mesh.RecalculateBounds();
		
		meshR.material = prismMat;			
	}
	#endregion	
		
	#region GL.LINES
	//these are visible in webplayer
	
	static Material lineMaterial;
	static private void CreateLineMaterial(){
		if( !lineMaterial ) {
			lineMaterial = new Material(
				"Shader \"Lines/Colored Blended\" {" +
				"SubShader { Pass { " +
				"    Blend SrcAlpha OneMinusSrcAlpha " +
				"    ZWrite Off Cull Off Fog { Mode Off } " +
				"    BindChannels {" +
				"      Bind \"vertex\", vertex Bind \"color\", color }" +
				"} } }" );
			lineMaterial.hideFlags = HideFlags.HideAndDontSave;
			lineMaterial.shader.hideFlags = HideFlags.HideAndDontSave;
		}
	}

	private	bool postRender = true;
	private void OnPostRender(){
		if(postRender){
			CreateLineMaterial();
			lineMaterial.SetPass( 0 );
			
			/*
			GL.Begin( GL.LINES );;
			GL.Color( new Color(0F,0F,1F,0.5F) );
			
			if(!invert){//highlight triangle edges
				for(int i = 0; i<visMesh.triangles.Length; i+=3){
					if((i%6)<3)	{ GL.Color( new Color(0F,1F,0F,0.5F) );	}
					else		{ GL.Color( new Color(0F,0F,1F,0.5F) );	}
					GL.Vertex3( visMesh.vertices[visMesh.triangles[i  ]].x, visMesh.vertices[visMesh.triangles[i  ]].y, visMesh.vertices[visMesh.triangles[i  ]].z );
					GL.Vertex3( visMesh.vertices[visMesh.triangles[i+1]].x, visMesh.vertices[visMesh.triangles[i+1]].y, visMesh.vertices[visMesh.triangles[i+1]].z );
					GL.Vertex3( visMesh.vertices[visMesh.triangles[i+2]].x, visMesh.vertices[visMesh.triangles[i+2]].y, visMesh.vertices[visMesh.triangles[i+2]].z );
				}
			}else{//highlight quad (2xtriangle) edges
				for(int i = 0; i<visMesh.triangles.Length; i+=3){
					if((i%12)<6)	{ GL.Color( new Color(0F,1F,0F,0.5F) );	}
					else			{ GL.Color( new Color(0F,0F,1F,0.5F) );	}
					GL.Vertex3( visMesh.vertices[visMesh.triangles[i  ]].x, visMesh.vertices[visMesh.triangles[i  ]].y, visMesh.vertices[visMesh.triangles[i  ]].z );
					GL.Vertex3( visMesh.vertices[visMesh.triangles[i+1]].x, visMesh.vertices[visMesh.triangles[i+1]].y, visMesh.vertices[visMesh.triangles[i+1]].z );
					GL.Vertex3( visMesh.vertices[visMesh.triangles[i+2]].x, visMesh.vertices[visMesh.triangles[i+2]].y, visMesh.vertices[visMesh.triangles[i+2]].z );
				}
			}
			GL.End();
			*/
			
			/*
			GL.Color( new Color(0F,0F,1F,0.5F) );			
			//highlight triangle edges
			for(int i = 0; i<visMesh.triangles.Length; i+=3){
				
				GL.Begin( GL.TRIANGLES );
				if((i%6)<3)	{ GL.Color( new Color(0F,1F,0F,0.5F) );	}
				else		{ GL.Color( new Color(0F,0F,1F,0.5F) );	}
				
				GL.Vertex3( visMesh.vertices[visMesh.triangles[i  ]].x, visMesh.vertices[visMesh.triangles[i  ]].y, visMesh.vertices[visMesh.triangles[i  ]].z );
				GL.Vertex3( visMesh.vertices[visMesh.triangles[i+1]].x, visMesh.vertices[visMesh.triangles[i+1]].y, visMesh.vertices[visMesh.triangles[i+1]].z );
				GL.Vertex3( visMesh.vertices[visMesh.triangles[i+2]].x, visMesh.vertices[visMesh.triangles[i+2]].y, visMesh.vertices[visMesh.triangles[i+2]].z );
				GL.End();
			}*/
									
			
			
			if(drawOutput){
				GL.Begin( GL.LINES );
				GL.Color( new Color(1F,1F,0F,0.5F) );
				for(int i = 0; i<postRenderOutput.Count; i+=2){
					GL.Vertex3( postRenderOutput[i  ].x, postRenderOutput[i  ].y, postRenderOutput[i  ].z);
					GL.Vertex3( postRenderOutput[i+1].x, postRenderOutput[i+1].y, postRenderOutput[i+1].z);
				}			
				GL.End();
			}
			//DrawPolygons(ref staticBlockers);			DrawPolygons(ref randomPolys);		
			if(drawPolygons){			
				for(int ip = 0; ip < staticBlockers.Length; ip++){	//polygon
					GL.Begin( GL.LINES );
					GL.Color( new Color(0F,1F,1F,0.5F) );
					for(int iv = 0; iv < staticBlockers[ip].vertices.Length; iv++){	//vertices of the polygon
						int nv = (iv+1)%staticBlockers[ip].vertices.Length; //next vertex
						GL.Vertex3( staticBlockers[ip].vertices[iv].x, staticBlockers[ip].vertices[iv].y, staticBlockers[ip].vertices[iv].z);
						GL.Vertex3( staticBlockers[ip].vertices[nv].x, staticBlockers[ip].vertices[nv].y, staticBlockers[ip].vertices[nv].z);
					}
					GL.End();
				}
				
				for(int ip = 0; ip < randomPolys.Length; ip++){	//polygon
					GL.Begin( GL.LINES );
					GL.Color( new Color(0F,1F,1F,0.5F) );
					for(int iv = 0; iv < randomPolys[ip].vertices.Length; iv++){	//vertices of the polygon
						int nv = (iv+1)%randomPolys[ip].vertices.Length; //next vertex
						GL.Vertex3( randomPolys[ip].vertices[iv].x, randomPolys[ip].vertices[iv].y, randomPolys[ip].vertices[iv].z);
						GL.Vertex3( randomPolys[ip].vertices[nv].x, randomPolys[ip].vertices[nv].y, randomPolys[ip].vertices[nv].z);
					}
					GL.End();
				}
			}
		}
	}
	
	#endregion
	
}
