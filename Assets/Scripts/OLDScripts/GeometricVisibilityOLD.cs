using UnityEngine;
using System.Collections;
//List Usage
using System.Collections.Generic;

/*	GeometricVisibility
	
	DRAWLINE - Visualization
		Gizmos to help visualizing the steps

	POLYGON EXTRACTION - Preparing Scene
		Extracts Polygons (list of edge points in a specific order)
		Polys of Walls and Static Objects of the Scene should be only calculated on startup for saving processing power
		Moving Objects should be kept in a seperate List that updates each frame
		
	Recurrung Calculations:	
	SEGMENTS
		Faces of the polygons that have the lightsource in front of them are gathered and grouped to segments
	
	DROP SEGMENTS
		To reduce the amount of segments that need to be checked we drop as much as possible with a cheap intial check
	
	SEGMENT LINES
		Remaining Segments are further splitted into seperate Lines for detailed intersection/occlusion tests
		Until here we only needed angle comparison. This is why we used pseudo angles since they are faster to compute and compare like the correct angles
		Now we calculate and store angles with the correct radian value
		
	DROP SEGMENT LINES
		

*/

public class GeometricVisibilityOLD : MonoBehaviour {
	
	#region	VARIABLES
	
	private	Camera cam;			//camera, for panning zooming etc.
	public	Transform source;	//the Transform of the lightSource, for position setting/getting
	public	Transform testSphere;
	
	//shadow caster GameObjects
	GameObject[] walls;
		
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
				
		//corresponding Renderer, used when gathering the polys: Renderer.isVisible, does not work for SkyNox (uses 2 cameras)
		//public Renderer renderer;
	}
	
	#endregion
	
	#region VARIABLES SERUMAS
	
		//Note for me: Consider class instead of struct, passed by reference
	
		private	List<Segment> segments = new List<Segment>();//all segments after gathering
		private float pi2 = 2F*Mathf.PI;
		private float pi = Mathf.PI;
	
		//static int maxLength = 50;	//maximum edges of a Poly
		private	class Segment{																								//translation language: Lithuanian	
			public float						segmin,segmax;			//minimalus ir maksimalus atstumas		//marrt/	minimum and maximum distance
			public float						start,end;				//startiniai pradzios ir galo kampai	//marrt/	ENTRY start and end angles
			public float						startnew,endnew;		//nauji pradzios ir galo kampai			//marrt/	new start and end angles
			public float						startd,endd;			//pradzios ir galo nuotoliai			//marrt/	start and end distances
			public bool							active = true;			//aktyvumas								//marrt/	activity
			public List<Vector3>				vert;					//vektoriai								//marrt/	vectors
			public int							vertCnt;				//vektoriu kiekis						//marrt/	vector points
				
			public VisibilityFader fader;
			public Transform transform;
		
			public bool right = false;	//is the segment intersecting with x axis of the source?
			public float radLenght;		//angle obstructed by this segment = end-start
		}
		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		struct LINE_ONE_CUT
		{
			public float						distance;
			public float						angle;
			public Vector2						ret;
		}
		//private struct CUT_less {bool operator ()(LINE_ONE_CUT const& a, LINE_ONE_CUT const& b) const {return a.angle<b.angle;}};
		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		struct LINE_INTERVAL{
			public float						start,end;
			public bool						active;
		}
		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		struct RESULT_POINTS{
			public float						angle,distance;
		}
		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		private	class SegmentLine{
			public Vector3						vec0;				//linijos vektoriai							//marrt/	line vectors
			public Vector3						vec1;
			public float						min,max;			//minimalus ir maksimalus atstumas 			//marrt/	minimum and maximum distance
			public float						start,end;			//startiniai pradzios ir galo kampai		//marrt/	ENTRY start and end angles
			public float						startnew,endnew;	//nauji pradzios ir galo kampai				//marrt/	new start and end angles
			public float						startd,endd;		//pradzios ir galo nuotoliai				//marrt/	start and end distances
			public float						l;					//linijos ilgis								//marrt/	The length
			public bool							active = true;		//aktyvus									//marrt/	activity
			public int							cut;				//cut array number							//marrt/	cut array number
			public List<LINE_INTERVAL>			intervals;
		}
		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		struct LINE_CUTS{
			public SegmentLine				line;
			public List<LINE_ONE_CUT>			rets;
		}
		
	#endregion	
	
	#region	STARTUP & UPDATE
	
	//called once after game starts
	private	void Awake(){
		cam = transform.camera;
	}
	
	//called once after Awake
	private	void Start(){
		walls = GameObject.FindGameObjectsWithTag("Wall");
		
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
		
		//for drawing helper lines we have to offset vectors to be in the right coordinate space because:
		drawOrigin = source.position; //everything is calculated with the lightsource as 0/0 origin
						
		//Struct				//extracted polygons						//generated polygons without gameobject
		if(drawLineToVertices)	{ DrawLineToVertices(ref staticBlockers);	DrawLineToVertices(ref randomPolys);}
		if(drawPolygons)		{ DrawPolygons(ref staticBlockers);			DrawPolygons(ref randomPolys);		}
		if(drawVisibleFaces)	{ DrawVisibleFaces(ref staticBlockers);		DrawVisibleFaces(ref randomPolys);	}
		
		if(avEnabled)			{ VisualizeAngles();															}
		
		//empty list and Update it
		segments.Clear();
		segmentLines.Clear();
		
		//functionality enable/disable
		segmentCount = 0;
		bool cheapGather = gatherTop||gatherBottom? false:true;
		
		bool fullPass = false; //fully pass all edges
		if (fullPass){
				
		}else if(gatherSegments){
			
			if(cheapGather){
				CheapGather(ref staticBlockers);
				CheapGather(ref randomPolys);
			}else{
				if(gatherTop)			{ GatherSegments(ref staticBlockers, true);		GatherSegments(ref randomPolys, true);	}
				if(gatherBottom)		{ GatherSegments(ref staticBlockers, false);	GatherSegments(ref randomPolys, false);	}
			}
		}
		
		if(dropSegments)		{ DropSegments();					}
		if(convertLines)		{ ConvertSegmentsToLines();			}
		
	//2nd approach interrupts here and submits segmentLines:
		VisibilityOLD vis = GetComponent<VisibilityOLD>();
		
		vis.Flush();
		if (fullPass){
			foreach(BottomPolygon b in staticBlockers){
				float x = source.position.x;
				float y = source.position.z;
				for(int i = 0; i<b.vertices.Length-1; i++){
					vis.addSegment(b.vertices[i].x-x, b.vertices[i].z-y, b.vertices[i+1].x-x, b.vertices[i+1].z-y);
				}
				vis.addSegment(b.vertices[b.vertices.Length-1].x-x, b.vertices[b.vertices.Length-1].z-y, b.vertices[0].x-x, b.vertices[0].z-y);
			}
			foreach(BottomPolygon b in randomPolys){
				float x = source.position.x;
				float y = source.position.z;
				for(int i = 0; i<b.vertices.Length-1; i++){
					vis.addSegment(b.vertices[i].x-x, b.vertices[i].z-y, b.vertices[i+1].x-x, b.vertices[i+1].z-y);
				}
				vis.addSegment(b.vertices[b.vertices.Length-1].x-x, b.vertices[b.vertices.Length-1].z-y, b.vertices[0].x-x, b.vertices[0].z-y);
			}
		}else if(cheapGather){
			linecount2ndApp = 0;
			foreach(Segment s in segments){
				for(int i = 0; i<s.vert.Count; i+=2){
					vis.addSegment(s.vert[i].x, s.vert[i].z, s.vert[i+1].x, s.vert[i+1].z);
					//vis.addSegment(s.vert[i+1].x, s.vert[i+1].z, s.vert[i].x, s.vert[i].z);
					linecount2ndApp++;
				}
			}
		}else{
			if(dropSegments){	//submit before drop (check performance)
				linecount2ndApp = 0;
				//Vector3 s = source.position;
				foreach(SegmentLine sl in segmentLines){
					vis.addSegment(sl.vec0.x, sl.vec0.z, sl.vec1.x, sl.vec1.z);
					//vis.addSegment(sl.vec0.x -s.x, sl.vec0.z -s.z, sl.vec1.x -s.x, sl.vec1.z -s.z);
					linecount2ndApp++;
				}						
			}else{				//or submit after drop (check performance)
				linecount2ndApp = 0;
				foreach(Segment s in segments){
					for(int i = 0; i<s.vert.Count-1; i++){
						vis.addSegment(s.vert[i].x, s.vert[i].z, s.vert[i+1].x, s.vert[i+1].z);
						//vis.addSegment(s.vert[i+1].x, s.vert[i+1].z, s.vert[i].x, s.vert[i].z);
						linecount2ndApp++;
					}
				}
			}
		}
		
		vis.InitCalc(source.position);
	// /2nd approach	
		
	//everything here does not as it should, skippable for now
		
		if(cutLines)			{ CutSegmentsLines();				}
		if(dropLines)			{ DropLines();						}
		
		//drawing Gizmo lines
		if(drawSegments)		{ DrawSegmentLines(ref segments);	}
		if(drawLines)			{ DrawLines(ref segmentLines);		}
		if(drawCuts)			{ }
		if(drawRecalc)			{ }
		if(drawFinal)			{ }
				
		if(autoGen)				{ CreateRandomPolygon(ref randomPolys);}
		
		//Debug.Log("Segments"+segments.Count+"||segmentLines:"+segmentLines.Count);
				
		/*float angle = Mathf.Atan2(testSphere.position.z-source.position.z,testSphere.position.x-source.position.x)* Mathf.Rad2Deg;
		if(angle<0){angle += 360F;}
		Debug.Log(angle);*/
				
		//orthogonal cross: center of map
		Debug.DrawLine(new Vector3(-250F,0F,0F),new Vector3(+250F,0F,0F),new Color(1F,1F,1F,0.09F));
		Debug.DrawLine(new Vector3(0F,0F,-250F),new Vector3(0F,0F,+250F),new Color(1F,1F,1F,0.09F));
		//orthogonal cross: center of light source
		Debug.DrawLine(new Vector3(-250F,0F,drawOrigin.z),new Vector3(+250F,0F,drawOrigin.z),new Color(1F,1F,1F,0.075F));
		Debug.DrawLine(new Vector3(drawOrigin.x,0F,-250F),new Vector3(drawOrigin.x,0F,+250F),new Color(1F,1F,1F,0.075F));
	}
	#endregion
	
	#region	DRAWLINE
	private	Vector3 drawOrigin = Vector3.zero;
	//just to see the order of the vertices, different colors to see order
	private	void DrawLineToVertices(ref BottomPolygon[] poly){
		for(int ip = 0; ip < poly.Length; ip++){	//polygon
			for(int iv = 0; iv < poly[ip].vertices.Length; iv++){	//vertices of the polygon
				Color color = new Color(0F,0F,1F,0.2F);
				if(iv == 0){color = new Color(0F,0.5F,1F,1F);}
				if(iv == 1){color = new Color(0F,0.5F,1F,0.5F);}
				Debug.DrawLine(drawOrigin, poly[ip].vertices[iv], color, 0F, false);
			}
		}
	}
	
	//draw the polygon CYAN
	private	void DrawPolygons(ref BottomPolygon[] poly){
		for(int ip = 0; ip < poly.Length; ip++){	//polygon
			for(int iv = 0; iv < poly[ip].vertices.Length; iv++){	//vertices of the polygon
				int nv = (iv+1)%poly[ip].vertices.Length; //next vertex
				Debug.DrawLine(poly[ip].vertices[iv], poly[ip].vertices[nv], new Color(0F,1F,1F,0.4F), 0F, false);
			}
		}
	}
	
	private	void DrawVisibleFaces(ref BottomPolygon[] poly){
		for(int ip = 0; ip < poly.Length; ip++){	//polygon
			for(int iv = 0; iv < poly[ip].vertices.Length; iv++){	//vertices of the polygon
				int nv = (iv+1)%poly[ip].vertices.Length; //next vertex
				
				Vector3 vertexDirection = poly[ip].vertices[iv]-poly[ip].vertices[nv];
				Vector3 sourceDirection = source.position-poly[ip].vertices[iv];
				if(!invisibleFaces){
					if( AngleDir(vertexDirection,sourceDirection,Vector3.up)<0F ){
						Debug.DrawLine(poly[ip].vertices[iv], poly[ip].vertices[nv], Color.white,0F,false);
					}
				}else{
					if( AngleDir(vertexDirection,sourceDirection,Vector3.up)>0F ){
						Debug.DrawLine(poly[ip].vertices[iv], poly[ip].vertices[nv], Color.white,0F,false);
					}
				}
			}
		}
	}
	
	private	void DrawSegmentLines(ref List<Segment> segments){
		for(int iSe = 0; iSe < segments.Count; iSe++){		//loop through[]
			Color colorV = new Color(0F,1F,0F,0.2F); //vertices
			Color colorF = new Color(0F,1F,0F,0.8F); //faces
			if(!segments[iSe].active){
				colorV = new Color(1F,0F,0F,0.2F);
				colorF = new Color(1F,0F,0F,0.8F);
			}
			//lines to vertices
			//for(int ise = 0; ise < segments[iSe].vert.Count; ise++){
			//	Debug.DrawLine(drawOrigin, segments[iSe].vert[ise], colorV, 0F, true);
			Debug.DrawLine(drawOrigin, segments[iSe].vert[0]+drawOrigin, colorV, 0F, true);
			Debug.DrawLine(drawOrigin, segments[iSe].vert[segments[iSe].vert.Count-1]+drawOrigin, colorV, 0F, true);
			//}
			//highlight faces
			for(int i = 1; i<segments[iSe].vertCnt; i++){
				Debug.DrawLine(segments[iSe].vert[i-1]+drawOrigin, segments[iSe].vert[i]+drawOrigin, colorF,0F,false);
			}
		}
	}
	
	private	void DrawLines(ref List<SegmentLine> segmentLines){
		for(int iSe = 0; iSe < segmentLines.Count; iSe++){		//loop through[]
			Color colorV = new Color(0F,1F,0.5F,0.25F); //vertices
			Color colorF = new Color(0F,1F,0.5F,0.75F); //faces
			if(!segmentLines[iSe].active){Debug.Log("LALALLAALLALAALx");
				colorV = new Color(1F,0F,0.5F,0.25F);
				colorF = new Color(1F,0F,0.5F,0.75F);
			}
			
			//lines to vertices
			Debug.DrawLine(drawOrigin, segmentLines[iSe].vec0+drawOrigin, colorV, 0F, true);
			Debug.DrawLine(drawOrigin, segmentLines[iSe].vec1+drawOrigin, colorV, 0F, true);
			
			//highlight faces
			Debug.DrawLine(segmentLines[iSe].vec0+drawOrigin, segmentLines[iSe].vec1+drawOrigin, colorF,0F,true);
		}
	}
	#endregion
		
	#region	POLYGON EXTRACTION
	private	void GeneratePolygonStructArr(ref BottomPolygon[] poly){
		poly = new BottomPolygon[walls.Length];
		
		int iPoly = 0;	//polygon integrator
		foreach(GameObject wall in walls){
			
			//Save Transform reference
			poly[iPoly].transform = wall.transform;
			
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
						
			//Not implemented yet
			//poly[iPoly].position = CalculateCenter(poly[iPoly].vertices);
			//poly[iPoly].relevantRadius = CalculateRadius(poly[iPoly].vertices);
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
	
	//mybe needed... http://stackoverflow.com/questions/5271583/center-of-gravity-of-a-polygon
	private	Vector3 CalculateCenter(ref Vector3[] polygon){
		return Vector3.zero;
	}
	
	//smallest circle problem
	private	float CalculateRadius(ref Vector3[] polygon){
		return 0F;
	}
	#endregion
	
	#region	RANDOM POLYGON

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
	}
	
	private	void ClearRandomPolygons(ref BottomPolygon[] polyArr){
		polyArr = new BottomPolygon[0];
	}
	
	private	void RefreshRandPolygons(ref BottomPolygon[] polyArr){
		//check Order
		if(checkCCW){
			for(int i = 0; i < polyArr.Length; i++){
				polyArr[i].vertices = FixCCWOrder(polyArr[i].vertices);
			}
		}
	}
	
	private	bool RandBool(){return (Random.value > 0.5f);}//just a random bool
	#endregion
	
	
	//SERUMAS
	#region SEGMENTS
	
	//cheaper 360° Gather for 2nd approach, without the top/bottom distinction and cut
	private	void CheapGather(ref BottomPolygon[] poly){
		Vector3 off = source.position;
		for(int ip = 0; ip < poly.Length; ip++){	//polygon
			
			Segment newSeg = new Segment();	
			newSeg.vert = new List<Vector3>();
			
			int length = poly[ip].vertices.Length;
			if(!invisibleFaces){
				for(int iv = 0; iv < length; iv++){	//vertices of the polygon
					//int nv = (iv<length-1)? iv+1 : 0; //next vertex
					int nv = (iv+1)%length;
					Vector3 vertexDirection = poly[ip].vertices[iv]-poly[ip].vertices[nv];
					Vector3 sourceDirection = source.position-poly[ip].vertices[iv];
										
					if( AngleDir(vertexDirection,sourceDirection,Vector3.up)<0F ){
						newSeg.vert.Add(poly[ip].vertices[iv]-off);
						newSeg.vert.Add(poly[ip].vertices[nv]-off);
					}
				}
			}else{
				for(int iv = 0; iv < length; iv++){	//vertices of the polygon
					int nv = (iv+1)%length;
					Vector3 vertexDirection = poly[ip].vertices[iv]-poly[ip].vertices[nv];
					Vector3 sourceDirection = source.position-poly[ip].vertices[iv];
										
					if( AngleDir(vertexDirection,sourceDirection,Vector3.up)>0F ){
						newSeg.vert.Add(poly[ip].vertices[iv]-off);
						newSeg.vert.Add(poly[ip].vertices[nv]-off);
					}
				}
			}			
			segments.Add(newSeg);
		}		
	}
	
	
	string[]segmentValues = new string[2];	//for debug, enable only one gameObject
	int		segmentCount;
	private	void GatherSegments(ref BottomPolygon[] poly, bool top){	//top or bottom half of poly
		
		Vector3 s = source.position;
		//Vector3 s = Vector3.zero;
		
		Segment newSeg = new Segment();
		newSeg.vert = new List<Vector3>();
		newSeg.segmin = 6500F;
		newSeg.segmax = 0F;
				
		for(int ip = 0; ip < poly.Length; ip++){	//polygon

			//future: check if this poly can be dropped immidiately
			//if( Vector3.Distance(polys[i].position, source.position) < radius + polys[i].relevantRadius){
			//if( ((polys[iP].position-position).sqrMagnitude - polys[iP].relevantRadius*polys[iP].relevantRadius) < radius*radius){
			//	continue;	//next poly
			//}
			
			bool lastVisible = false;	//if last checked face was visible
			int vertexCount = 0;
						
			int length = poly[ip].vertices.Length;
			bool[] visFaces = new bool[length];
			//check visible faces
			for(int iv = 0; iv < length; iv++){
				int nv = (iv+1)%length;
				Vector3 curV = poly[ip].vertices[iv];
				Vector3 nexV = poly[ip].vertices[nv];
				
				//visFaces[iv] = (curV.z*nexV.x-curV.x*nexV.z) < 0F;
				Vector3 vertexDirection = curV-nexV;
				Vector3 sourceDirection = s-curV;
				
				visFaces[iv] = false;
				//is the face on the right half and between this and next vertex visible?
				if(top){
					if(curV.z-s.z>0F || nexV.z-s.z>0F){//if at least one vertix of the line is in the right half
						visFaces[iv] = TestFaceVisibility(ref vertexDirection, ref sourceDirection);
					}
				}else{
					if(curV.z-s.z<0F || nexV.z-s.z<0F){
						visFaces[iv] = TestFaceVisibility(ref vertexDirection, ref sourceDirection);
					}
				}
			}
			
			//find any invisible vertex to start from, because segments overlapping first vertex would create 2 seperate segments
			int offset = 0;
			for(int iv = 0; iv < length; iv++){
					if(!visFaces[iv]){offset = iv+1; break;};
			}
			
			//for testing visFaces...
			//Debug.DrawLine(poly[ip].vertices[offset], source.position, Color.cyan,0F,false);
			//for(int iv = offset; iv < (length+offset); iv++){
			//	if(visFaces[iv%length])
			//	Debug.DrawLine(poly[ip].vertices[iv%length], poly[ip].vertices[(iv+1)%length], Color.green,0F,false);
			//}
			
			//needed fpr pseudoangles inaccuracys on Cutlines that are all very close
			//otherwise the drop rate of segments on horizonatal lines would suffer
			
			//Intersection with the X-Axis?
			bool axisInterFromBelow = false;
			bool axisInterFromAbove = false;
			
			for(int iv = offset; iv < (length+offset); iv++){
				//from now on we save vector points with the light source as origin
				Vector3 curV = poly[ip].vertices[ iv	%length]-s;
				Vector3 nexV = poly[ip].vertices[(iv+1)	%length]-s;			
				if(visFaces[iv%length]){	//this step adds one line segment and one vertex (or two vertices if its the first)										
					//Cut line with horizon
					if(curV.z>0.0f != nexV.z>0.0f){ //horizon is cut if the signs of the 2 vectice's z values don't match
						float	h1	= -curV.z;
						Vector3	hv	= nexV-curV;
						if(top && hv.z<0.0f || !top && hv.z>0.0f){
							nexV.x -= hv.x * (hv.z-h1)/hv.z;	//x-axis intersection x
							nexV.z	= 0.0f;						//x-axis intersection y (zero of course)
							axisInterFromAbove = true;			//line has intersected X-axis from ABOVE the axis
							//Debug.DrawLine(drawOrigin, nexV+drawOrigin,Color.blue,0F,false);
						}else{
							curV.x += hv.x * h1/hv.z;			//x-axis intersection x
							curV.z	= 0.0f;						//x-axis intersection y (zero of course)
							axisInterFromBelow = true;			//line has intersected X-axis from BELOW the axis
							//Debug.DrawLine(drawOrigin, curV+drawOrigin,Color.red,0F,false);
						}
					}else{
						axisInterFromBelow = false;
						axisInterFromAbove = false;
					}
					float distance = curV.magnitude;
					//first vertex
					if(vertexCount == 0){
						newSeg.vert.Add(curV);	//add first vertex
						vertexCount++;
							//angle(radian) to first point
							//newSeg.start = Mathf.Atan2(curV.z,curV.x);// * Mathf.Rad2Deg;
							//if(newSeg.start<0){newSeg.start += pi2;}	//fix to positive 0-360° representation
							//newSeg.start = pi2-newSeg.start;//invert angle count direction
						if(axisInterFromBelow){
							newSeg.start = -1.0f;
						}else{
							newSeg.start = newSeg.startnew = PseudoAngle(curV,Lenght(curV));
							newSeg.startd = distance;
						}
					}
										
					newSeg.vert.Add(nexV);		//add next vertex
					vertexCount++;
										
					//Vector3 proj = Vector3.Project(-curV, curV-nexV)+curV;
					//newSeg.segmin = Min(newSeg.segmin, proj.magnitude);//substitute for LineMinDistance
						//if(ip == 0 && vertexCount == 2){//test MinDistance substitute
						//	Debug.DrawLine(curV+drawOrigin, nexV+drawOrigin,Color.blue,0F,false);
						//	Debug.DrawRay(drawOrigin,proj,Color.white,0F,false);
						//}//doesnt work, we would have to add a check if the projection hits the line
					
					//CheckXAxisIntersection(ref curV, ref nexV, ref newSeg.right);
					
					newSeg.segmin = Min(newSeg.segmin, LineMinDistance(curV,nexV,Vector3.zero));
					newSeg.segmin = Min(newSeg.segmin, distance);//it cannot happen that distance issmaller than lineMinDistance
					newSeg.segmax = Max(newSeg.segmax, distance);
					lastVisible = true;	//remember that last face was visible
				}else{	//this step ends the Segment
					if((vertexCount > 0) && lastVisible){	//if this is the first vertex after a segment
						float distance = curV.magnitude;
							//angle(radian) to last point
							//newSeg.end = Mathf.Atan2(curV.z,curV.x);// * Mathf.Rad2Deg;
							//if(newSeg.end<0){newSeg.end += pi2;}
							//newSeg.end = pi2-newSeg.end;//invert angle count direction
						
						if(axisInterFromAbove){
							newSeg.end = 1.0f;
						}else{
							newSeg.end = PseudoAngle(curV,Lenght(curV));
							newSeg.endd = distance;
						}						
						newSeg.segmin = Min(newSeg.segmin, distance);
						newSeg.segmax = Max(newSeg.segmax, distance);
						
						newSeg.vertCnt = vertexCount;
						
						//there may be multiple seperated segments on a poly, reset vertCnt for possible second segment on this poly
						vertexCount = 0;
														
						//angleFlip
						
						//pseudo angles
						//if(newSeg.start>newSeg.end){newSeg.end = 1.0f;}
						/*if(newSeg.start>newSeg.end){
							float end = newSeg.end;
							newSeg.end = newSeg.start;
							newSeg.start = end;
						}*/
						
						
						//normal angles
						
						//angle offset:
						//if(newSeg.right){newSeg.end += pi2;}// newSeg.start = pi2-newSeg.start;}
						//if(newSeg.right && newSeg.end<180F){newSeg.end += pi2;}//works only if segment is above
						//if(newSeg.right && newSeg.end>180F){newSeg.end -= pi2/2;}//
						
						//angleFlip
						/*if(newSeg.start>newSeg.end){
							float end = newSeg.end;
							newSeg.end = newSeg.start;
							newSeg.start = end;
						}*/
						
						
						newSeg.startnew = newSeg.start;
						newSeg.endnew = newSeg.end;
						
						newSeg.radLenght = newSeg.end-newSeg.start; //used to make the check independent from the angle jump (0to360°)
						
						//SAVE SEGMENT
						segments.Add(newSeg);
						segmentCount++;
						
						//debug
						if(showSegStats){
							int sv = 0;//which index of segmentValues
							if(segmentCount==2){sv = 1;}
							segmentValues[sv] = "Segment owner: random poly";
							if(poly[ip].transform){
								newSeg.transform = poly[ip].transform;
								segmentValues[sv] = "Segment"+sv+" owner: "+poly[ip].transform.name;
								if(poly[ip].fader){newSeg.fader = poly[ip].fader;}
							}
							
							segmentValues[sv] +=
								"\noffset\t\t"	+offset+
								"\nsegmin\t\t"	+newSeg.segmin+
								"\nsegmax\t\t"	+newSeg.segmax+
								"\nstart\t\t\t"	+newSeg.start		+//* Mathf.Rad2Deg+"(blue)"+
								"\nend\t\t\t"	+newSeg.end			+//* Mathf.Rad2Deg+"(yellow)"+
								//"\nstartnew\t\t"+newSeg.startnew	+//* Mathf.Rad2Deg+
								//"\nendnew\t\t"	+newSeg.endnew		+//* Mathf.Rad2Deg+
								"\nstartd\t\t\t"+newSeg.startd+
								"\nendd\t\t\t"	+newSeg.endd+
								"\nactive\t\t"	+newSeg.active+
								"\n\nlightpos\t"+s+
								"\nvertices:"+
								"\nvertCnt\t\t"	+newSeg.vertCnt +
								"\nvertices\t"+newSeg.vert[0];
							
							for(int i = 1; i<newSeg.vertCnt; i++){
								segmentValues[sv] += ("\n\t\t\t"	+newSeg.vert[i]);
							}
							//if(sv==0){
							//	Vector3 startDirection = Quaternion.Euler(new Vector3(0F, newSeg.start*Mathf.Rad2Deg, 0F)) * Vector3.right;
							//	Vector3 endDirection = Quaternion.Euler(new Vector3(0F, newSeg.end*Mathf.Rad2Deg, 0F)) * Vector3.right;
							//	Debug.DrawRay(drawOrigin,startDirection*3F,Color.blue,0F,false);
							//	Debug.DrawRay(drawOrigin,endDirection*3F,Color.yellow,0F,false);
									//Debug.DrawRay(drawOrigin,startDirection*newSeg.segmin,Color.blue,0F,false);
									//Debug.DrawRay(drawOrigin,endDirection*newSeg.segmax,Color.yellow,0F,false);
							//}
						}
						
						//create new Segment
						newSeg = new Segment();
						newSeg.vert = new List<Vector3>();
						newSeg.segmin = 6500F;
						newSeg.segmax = 0F;
					}
					lastVisible = false;
				}	
			}
		}
		segments.Sort((segment2, segment1) => (segment1.start.CompareTo(segment2.start)));//sort segments by the angle of the start vertex
		
		//string segmentList = "";
		//for(int i=0; i < segments.Count; i++){
		//	segmentList += "start\t"+segments[i].start+"\n";
		//}
		//Debug.Log(segmentList);
			
		/*if(showSegStats){
			segmentCount = segments.Count;
		}*/
	}
	
	
	private	bool TestFaceVisibility(ref Vector3 vertexDirection, ref Vector3 sourceDirection){
		if(!invisibleFaces){
			if( AngleDir(vertexDirection,sourceDirection,Vector3.up)<0F ){return true;}
		}else{
			if( AngleDir(vertexDirection,sourceDirection,Vector3.up)>0F ){return true;}
		}
		return false;
	}
	
	//check if a point is left or right to a direction vector, can be reduced for 2D only
	private	float AngleDir(Vector3 fwd, Vector3 targetDir, Vector3 up) {
		Vector3 perp = Vector3.Cross(fwd, targetDir);
		float dir	 = Vector3.Dot(perp, up);
		if		(dir > 0F)	{ return  1F;}//RIGHT
		else if	(dir < 0F)	{ return -1F;}//LEFT
		else				{ return  0F;}
	}
	
	private	float LineMinDistance(Vector3 v, Vector3 w, Vector3 p) {
		// Return minimum distance between line segment vw and point p
		float l2 = Vector3.SqrMagnitude(v-w);  // i.e. |w-v|^2 -  avoid a sqrt
		if (l2 == 0.0) return Vector3.Distance(p, v);   // v == w case
		// Consider the line extending the segment, parameterized as v + t (w - v).
		// We find projection of point p onto the line. 
		// It falls where t = [(p-v) . (w-v)] / |w-v|^2
		float t = Vector3.Dot(p - v, w - v) / l2;
		if (t < 0.0) return Vector3.Distance(p, v);       // Beyond the 'v' end of the segment
		else if (t > 1.0) return Vector3.Distance(p, w);  // Beyond the 'w' end of the segment
		Vector3 projection = v + t * (w - v);  // Projection falls on the segment
		return Vector3.Distance(p, projection);
	}
	
	private	void CheckXAxisIntersection(ref Vector3 curV, ref Vector3 nexV, ref bool right){
		//check if Line is crossing x-axis left or right of the light object (then we have the 360° to 0° jump and could get confused)		
		//determines the x value of the segment line refering to source at y=0, now we now if segment is left or right
		//if( (curV.z < 0F && nexV.z > 0F) || (curV.z > 0F && nexV.z < 0F)){
		if( (curV.z < 0F != nexV.z < 0F)){
			Debug.DrawLine(curV+drawOrigin,nexV+drawOrigin,Color.magenta,0F,false);
			float intersection = 0F;
			if((nexV.x - curV.x)!=0){//catches Div by 0
				intersection = (curV.x - nexV.x) * ((0F - nexV.z) / (curV.z - nexV.z)) + nexV.x;
				//Debug.Log("super: "+intersection);
			//normal Lerp with 0.5F because nexV.x-curV.x = 0;
			}else{
				intersection = (curV.x - nexV.x) * 0.5F + nexV.x;
				//Debug.Log("normal: "+intersection);
			}
			if(intersection > 0F){
				right = true;
			}
			//Debug.Log("rightIntersection:"+right);
			//if we are left, we have a jump in Angles, end will be smaller than start
		}
	}
	#endregion
		
	#region DROP SEGMENTS
		
	private	void DropSegments(){
		//int segmentCount = segments.Count;
		for(int b1=0; b1 < segmentCount-1; b1++){			//for every segment
			Segment it = segments[b1];						//Segment is a class: REFERENCE TYPE! it is the segment now, no copy
			if(it.active){
				for(int b2=b1+1; b2 < segmentCount; b2++){	//for every following segment
					Segment it_next = segments[b2];
					if(it_next.start >= it.end){break;}		//??, could be a abort condition if sorting is wrong
					if(it_next.active){
						Segment far;					//far
						Segment near;					//near
						if(it_next.segmin >= it.segmax){		//if next segments nearest point is nearer than currents farthest
							far=it_next;near=it;				//far segment is next, near segment is current
						}else if(it.segmin >= it_next.segmax){
							near=it_next;far=it;
						}else continue;		//happens if segments lay within same distance somewhere, then segment stays fully active, we have to check individual lines						
						if(far.startnew	>= near.start		&& far.endnew	<= near.end	){	//1. if the far segment is completely hidden by the UNMODIFIED near segment
							far.active=false;
						}else
						if(far.startnew	>= near.startnew	&& far.endnew	<= near.endnew	){	//2. if the far segment is completely hidden by the MODIFIED near segment
							far.active=false;
						}else
						if(far.startnew	>= near.startnew	&& far.startnew	<  near.endnew	){	//3. if far segment is partly hidden (at its start) by near
							if(near.endnew >= far.endnew){			//if end is also hidden
								far.active=false;
							}else far.startnew = near.endnew;		//if not, the new start of the far segment is the near's end
						}else
						if(far.endnew	> near.startnew	&& far.endnew	<= near.endnew	){	//4. if far segment is partly hidden (at its end) by near
							if(far.startnew >= near.startnew){		//if start is also hidden
								far.active=false;
							}else far.endnew = near.startnew;		//if not, the new end of the far segment is the near's start
						}
						//check if it is fading in in every case
						if(far.fader){	if(!far.active){far.fader.Fade(false);}else{far.fader.Fade(true);}	}
					}
				}
			}
		}
	}
	#endregion
		
	#region SEGMENT LINES
	
	private int	line_count = 0; //amountof lines
	List<SegmentLine> segmentLines = new List<SegmentLine>();
	private	void ConvertSegmentsToLines(){
				
		line_count=0;
		for(int iseg=0;iseg<segmentCount;iseg++) //for each Segment
		{
			Segment seg = segments[iseg];	//Segment is a class: REFERENCE TYPE! it is the segment now
			if(seg.active && seg.vertCnt>1){	//if the segment is still active
								
				for(int ls = 1; ls != seg.vertCnt; ls++){ //for each Point in the Segement -1 creates one lineSegment
					
					SegmentLine line = new SegmentLine();
					
					//line vectors
					line.vec0 = seg.vert[ls-1];
					line.vec1 = seg.vert[ls];
					
					//ENTRY start and end angles/new start and end angles
					//Bottom half yields negativ values, Top-positive
					//	however, for -pi we get +pi for the bottom half (-pi is same rotation as +pi), therefore Abs()
					line.start	= line.startnew	= Mathf.Abs( NormalAngle(line.vec0) );
					line.end	= line.endnew	= Mathf.Abs( NormalAngle(line.vec1) );
					//Debug.Log("start:"+line.start.ToString("F2")+"|end:"+line.end.ToString("F2"));
					
					//start, end, min & max distances
					line.startd	= line.vec0.magnitude;
					line.endd	= line.vec1.magnitude;
					line.min	= Min(line.endd, line.startd);
					line.max	= Max(line.endd, line.startd);
					line.min	= Min(line.min, LineMinDistance(seg.vert[ls-1],seg.vert[ls],Vector3.zero));
					
					//length
					//line.l		= (line.vec0 - line.vec1).magnitude;
					Vector2 ln			= line.vec1-line.vec0;
					line.l				= LENGTH_(ln);
					
					
					line.active	= true;		//activity
					line.cut	= -1;		//cut array number
					//line.intervals = 0;
					
					segmentLines.Add(line);
					line_count++;
				}
			}
		}		
		//sort by start
		segmentLines.Sort((segment2, segment1) => (segment1.start.CompareTo(segment2.start)));//sort segmentLiness by the angle of the start vertex
		
				
		//
		/*
		string sortTest = "";
		for(int i=0; i < segmentLines.Count; i++){sortTest += ""+segmentLines[i].start.ToString("F2")+"\n";}
		Debug.Log(sortTest);
		//*/
	}
		
	
	SegmentLine	tmp_lin;
	List<SegmentLine> lin_dyn = new List<SegmentLine>();
	List<SegmentLine> lin_lst = new List<SegmentLine>();
	private	void ConvertSegmentsToLines2(){//original SegmentLine Conversion from Serumas ported to c#
		
		Debug.Log(line_count);
		
		line_count=0;
		for(int b1=0;b1<segmentCount;b1++) //for every SEGMENT
		{
			Segment it = segments[b1];			//Segment is a class: REFERENCE TYPE! it is the segment now
			if(it.active){	//if the segment is still active
				
				//serumas uses predefined array initialized with MAX-Length
				//dont know, but maybe to reduce creation overhead
				Vector3[] tmpvec		= new Vector3[500];
				float[] tmpflt			= new float[500];
				float[] tmpflt2			= new float[500];
				
				//precalculating values that will be needed for a check
				for(int ii=0;ii<it.vertCnt;ii++)
				{
					Vector3 calc_vec	= -it.vert[ii];
					float distance		= Lenght(calc_vec);
					
					tmpvec[ii]				= calc_vec;
					tmpflt[ii]				= distance;
					tmpflt2[ii]				= PseudoAngle(calc_vec, distance);
				}
				float ba= NormalAngle(tmpvec[0]);bool eba=true;	//the first vectors angle
				for(int ii=1;ii<it.vertCnt;ii++)	//for every vertex of this segment except the first( [0] )
				{
					//	checking the 2 vectors of the line with itself, only adding line IF:
					//	startnew <= pseudo1 && pseudo1 <= endnew	(vertex1 within new bounds?)
					//	OR
					//	startnew <= pseudo0 && pseudo0 <= endnew	(vertex0 within new bounds?)
					//	OR
					//	startnew >= pseudo0 && pseudo1 >= endnew	(both vertices within new bounds?)
					if((it.startnew<=tmpflt2[ii] && tmpflt2[ii]<=it.endnew)||(it.startnew<=tmpflt2[ii-1] && tmpflt2[ii-1]<=it.endnew)||(it.startnew>=tmpflt2[ii-1] && tmpflt2[ii]  >=it.endnew))
					{
						//if(line_count==lin_dyn.Count){lin_dyn.Add(tmp_lin);}//pushback= ADD
						SegmentLine	line = lin_dyn[line_count];
						
						//calc new line params
						line.vec0			= it.vert[ii];
						line.vec1			= it.vert[ii-1];
						Vector2 ln			= line.vec1-line.vec0;
						line.l				= LENGTH_(ln);
						line.active			= true;
						line.cut			= -1;
						line.startd			= tmpflt[ii-1];
						line.endd			= tmpflt[ii];
						if(!eba)ba			= NormalAngle(tmpvec[ii-1]); //if segment is partly obscured calc real angle
						line.startnew		= line.start		= ba;
						ba					= NormalAngle(tmpvec[ii]);if(ba==0.0f)ba=pi;eba=true; //-pi angle flip to pi compensation i guess
						line.endnew			= line.end			= ba;
						if(line.start>line.end)line.endnew=line.end=pi;
						line.min			= Min(tmpflt[ii],tmpflt[ii-1]);
						line.max			= Max(tmpflt[ii],tmpflt[ii-1]);
						line.min = Min(line.min, LineMinDistance(it.vert[ii-1],it.vert[ii],Vector3.zero));
						line.intervals.Clear();
						line_count++;
					}else eba=false; //line is shortened somehow (obscured)
				}
			}
		}
		//pointerize
		//if(lin_lst.Count<line_count)lin_lst.resize(line_count);
		for(int h1=0;h1<line_count;h1++)lin_lst[h1]=lin_dyn[h1];
		
		//sort by start
		
	}
	
	#endregion
	
	#region CUT SEGMENT LINES
	
	private List<LINE_CUTS> cuts = new List<LINE_CUTS>();
	//private int cut_s = 0;
	
	private LINE_CUTS tmp_cut;
	private LINE_ONE_CUT tmp_one_cut;
	
	private	void CutSegmentsLines()	{
		cuts.Clear();
		//cut_s = line_count;
		
		for(int b1=0; b1 < line_count-1; b1++){
			
			SegmentLine it = segmentLines[b1];
					
			for(int b2=b1+1; b2 < line_count; b2++)	{
				
				SegmentLine it_next = segmentLines[b1];
				
				if(it_next.start>=it.end)break;
				
				if(it_next.min<it.max && it.min<it_next.max){
					Vector2 ret = Vector2.zero;
					if(fast_line_intersection(it.vec0, it.vec1, it_next.vec0, it_next.vec1, ref ret)){
						//#ifdef VISIBILITY_STATS 
						//	stat_cuts++;
						//#endif
						if(it.cut==-1){
							it.cut = cuts.Count;
							tmp_cut.line	=it;
							cuts.Add(tmp_cut);//cuts.push_back(tmp_cut);
							cuts[it.cut].rets.Clear();
							tmp_one_cut.distance		= it.startd;					
							tmp_one_cut.angle			= it.start;
							tmp_one_cut.ret				= it.vec1;
							cuts[it.cut].rets.Add(tmp_one_cut);//cuts[it.cut].rets.push_back(tmp_one_cut);
							tmp_one_cut.distance		= it.endd;					
							tmp_one_cut.angle			= it.end;
							tmp_one_cut.ret				= it.vec0;
							cuts[it.cut].rets.Add(tmp_one_cut);//cuts[it.cut].rets.push_back(tmp_one_cut);
							it.active=false;
							//#ifdef VISIBILITY_STATS 
							//	stat_lines--;
							//#endif
						}
						
						if(it_next.cut==-1){
							it_next.cut=cuts.Count;
							tmp_cut.line	=it_next;
							cuts.Add(tmp_cut);//cuts.push_back(tmp_cut);
							cuts[it_next.cut].rets.Clear();
							tmp_one_cut.distance		= it_next.startd;					
							tmp_one_cut.angle			= it_next.start;
							tmp_one_cut.ret				= it_next.vec1;
							cuts[it_next.cut].rets.Add(tmp_one_cut);//cuts[it_next.cut].rets.push_back(tmp_one_cut);
							tmp_one_cut.distance		= it_next.endd;					
							tmp_one_cut.angle			= it_next.end;
							tmp_one_cut.ret				= it_next.vec0;
							cuts[it_next.cut].rets.Add(tmp_one_cut);//cuts[it_next.cut].rets.push_back(tmp_one_cut);
							it_next.active=false;
							//#ifdef VISIBILITY_STATS 
							//	stat_lines--;
							//#endif
						}
						/*const*/ Vector2 calc_vec	= -ret;
						tmp_one_cut.distance		= LENGTH_(calc_vec);					
						tmp_one_cut.angle			= Max(NormalAngle(calc_vec),0.0f);
						tmp_one_cut.ret				= ret;
						cuts[it.cut].rets.Add(tmp_one_cut);//cuts[it.cut].rets.push_back(tmp_one_cut);
						cuts[it_next.cut].rets.Add(tmp_one_cut);//cuts[it_next.cut].rets.push_back(tmp_one_cut);
					}
				}
			}
		}
		
		for(int r1=0;r1<cuts.Count;r1++){
			
			LINE_CUTS it = cuts[r1];	//LINE_CUTS * it=&cuts[r1];
			//std::sort(it.rets.begin(),it.rets.end(),CUT_less());
			
			//it.Sort((segment2, segment1) => (segment1.angle.CompareTo(segment2.angle)));//sort segments by the angle of the start vertex
		
			LINE_ONE_CUT last_cut = it.rets[0];//LINE_ONE_CUT * last_cut=&it.rets[0];
			
			for (int r2=1;r2<it.rets.Count;r2++){
				LINE_ONE_CUT rt = it.rets[r2];//LINE_ONE_CUT * rt=&it.rets[r2];
				
				//insert new line
				////if(line_count==lin_dyn.size()){lin_dyn.push_back(tmp_lin);lin_lst.push_back(NULL);}
				////SEGMENT_LINE *	line=&lin_dyn[line_count];
				
				//skip above and Add new line at the end
				SegmentLine line = new SegmentLine();
				
				//calc new line params
				line.vec1		= last_cut.ret;
				line.startd		= last_cut.distance;
				line.startnew		= line.start		= last_cut.angle;
				line.vec0		= rt.ret;
				line.endd			= rt.distance;
				line.endnew		= line.end			= rt.angle;
				line.active		= true;
				line.intervals.Clear();
				/*const*/ Vector2 ln	= line.vec1-line.vec0;
				line.l				= LENGTH_(ln);
				line.min			= Min(line.endd,line.startd);
				line.max			= Max(line.endd,line.startd);
				//line_min_distance(&line.vec[1],&line.vec[0],&line.min);
				LineMinDistance(line.vec1,line.vec0,Vector3.zero);//!!!!
				last_cut=rt;
				line_count++;
				
				segmentLines.Add(line);
				
				//#ifdef VISIBILITY_STATS 
				//	stat_cuts_new_lines++;
				//	stat_lines++;
				//#endif
			}
			it.rets.Clear();
		}
		//cut_e = line_count;//rendering only
		//pointerize
		//if(lin_lst.Count<line_count)lin_lst.resize(line_count);
		//for(int h1=0;h1<line_count;h1++)lin_lst[h1]=&lin_dyn[h1];
	}
	/*
	inline bool		fast_line_intersection(vector2 a1,vector2 a2,vector2 a3,vector2 a4,vector2 * ret=NULL);
		
	inline bool VIEWSYSTEM::fast_line_intersection(vector2 a1,vector2 a2,vector2 a3,vector2 a4,vector2 * ret)
	{
		float LowerX,UpperX,LowerY,UpperY;
		const float Ax = a2.x - a1.x;
		const float Bx = a3.x - a4.x;
		if(Ax<0.0f){LowerX = a2.x;UpperX = a1.x;}else {UpperX = a2.x;LowerX = a1.x;}
		if(Bx>0.0f){if(UpperX<a4.x || a3.x<LowerX)return false;}else if(UpperX<a3.x || a4.x<LowerX)return false;
		const float Ay = a2.y - a1.y;
		const float By = a3.y - a4.y;
		if(Ay<0.0f){LowerY=a2.y;UpperY=a1.y;}else{UpperY=a2.y;LowerY=a1.y;}
		if(By>0.0f){if(UpperY<a4.y || a3.y<LowerY)return false;}else if(UpperY<a3.y || a4.y<LowerY)return false;
		const float Cx= a1.x - a3.x;
		const float Cy= a1.y - a3.y;
		const float ddd=(By * Cx) - (Bx * Cy);
		const float f=(Ay * Bx) - (Ax * By);
		if(f>0.0f){if(ddd<0.0f || ddd>f)return false;}else if(ddd>0.0f || ddd<f)return false;
		const float e= (Ax * Cy) - (Ay * Cx);
		if(f>0.0f){if(e<0.0f || e>f)return false;}else if(e>0.0f || e<f)return false;
		if(!ret)return true;
		const float ua=ddd/f;  
		ret->x=a1.x+Ax*ua;
		ret->y=a1.y+Ay*ua;
		return true;
	}*/
	
	private bool fast_line_intersection(Vector2 a1,Vector2 a2,Vector2 a3,Vector2 a4,ref Vector2 ret){
		float LowerX,UpperX,LowerY,UpperY;
		/*const*/ float Ax = a2.x - a1.x;
		/*const*/ float Bx = a3.x - a4.x;
		if(Ax<0.0f){LowerX = a2.x;UpperX = a1.x;}else {UpperX = a2.x;LowerX = a1.x;}
		if(Bx>0.0f){if(UpperX<a4.x || a3.x<LowerX)return false;}else if(UpperX<a3.x || a4.x<LowerX)return false;
		/*const*/ float Ay = a2.y - a1.y;
		/*const*/ float By = a3.y - a4.y;
		if(Ay<0.0f){LowerY=a2.y;UpperY=a1.y;}else{UpperY=a2.y;LowerY=a1.y;}
		if(By>0.0f){if(UpperY<a4.y || a3.y<LowerY)return false;}else if(UpperY<a3.y || a4.y<LowerY)return false;
		/*const*/ float Cx= a1.x - a3.x;
		/*const*/ float Cy= a1.y - a3.y;
		/*const*/ float ddd=(By * Cx) - (Bx * Cy);
		/*const*/ float f=(Ay * Bx) - (Ax * By);
		if(f>0.0f){if(ddd<0.0f || ddd>f)return false;}else if(ddd>0.0f || ddd<f)return false;
		/*const*/ float e= (Ax * Cy) - (Ay * Cx);
		if(f>0.0f){if(e<0.0f || e>f)return false;}else if(e>0.0f || e<f)return false;
		//if(ret != null)return true;
		/*const*/ float ua=ddd/f;  
		ret.x = a1.x+Ax*ua;
		ret.y = a1.y+Ay*ua;
		return true;
	}
	
	#endregion
	
	#region DROP SEGMENT LINES
	
	private	void DropLines(){
		for(int b1=0; b1 < line_count-1; b1++){
			
			SegmentLine it = segmentLines[b1];
			if(it.active){
				
				for(int b2=b1+1;b2<line_count;b2++){
					
					SegmentLine it_next = segmentLines[b1];
					if(it_next.start >= it.end){Debug.Log("BREAK");break;}	//catch for sorting fail?
					
					if(it_next.active){
						
						SegmentLine far = null;
						SegmentLine near = null;
						//far = it_next; near = it;
						//near = it_next; far = it;
						//if(far != null){Debug.Log("NOTNULL");}else{Debug.Log("NULL");}
						
						if(it_next.min >= it.max){Debug.Log("AAA");
							//if the minimum distance of the next line is greater or equal then the maximum (no intersection), then it is far
							far = it_next;	near = it;
						}else if(it.min >= it_next.max){//otherwise it is near
							far = it;		near = it_next;	Debug.Log("BBBBBBBBBBB");
						}else{	//else the 2 lines lie within the same distance level somewhere, compare angles now
							if		( it.start == it_next.start && it.end == it_next.end){	//if angles are the same, unprobable
								if		( it.startd	<	it_next.startd)	{ far = it_next;	near = it;}			//if current's start angle is smaller then next it is near
								else if	( it.startd	>	it_next.startd)	{ far = it;			near = it_next;}	//otherwise its far
								else if	( it.endd	<	it_next.endd)	{ far = it_next;	near = it;}			//if current's start angle is smaller then next it is near
								else if	( it.endd	>	it_next.endd)	{ far = it;			near = it_next;}	//otherwise its far
							}else if( it.start < it_next.start){	//if current's start angle is smaller
								float hs = line_cut(it.l, it.startd, it.endd, it.end-it.start, it_next.start - it.start);	//
								if		( hs		<	it_next.startd)	{ far = it_next;	near = it;}			//
								else if	( hs		>	it_next.startd)	{ far = it;			near = it_next;}	//
								else{Debug.Log("ERROR1");/*error*/}
							}else{
								if( it_next.end <= it.end){	//if next's end angle is smaller or equal
									float he = line_cut(it.l, it.startd, it.endd, it.end-it.start, it_next.end - it.start);
									if		( he	<	it_next.endd)	{ far = it_next;	near = it;}
									else if	( he	>	it_next.endd)	{ far = it;			near = it_next;}
									else{Debug.Log("ERROR2");/*error*/}
								}else{	//else
									float he = line_cut(it_next.l, it_next.startd, it_next.endd, it_next.end - it_next.start, it.end - it_next.start);
									if		( he	<	it.endd)		{ far = it;			near = it_next;}
									else if	( he	>	it.endd)		{ far = it_next;	near = it;}
									else{Debug.Log("ERROR3");/*error*/}
								}
							}//Debug.Log("CCCCCCCCCCCCCC");
						}
						
						//if far has been assigned
						if(far != null){
							if( (far.startnew >= near.start && far.endnew <= near.end) || (far.startnew >= near.startnew && far.endnew <= near.endnew) ){
								far.active=false;
							}else if(far.startnew>=near.startnew && far.startnew<near.endnew){
								if(near.endnew>=far.endnew){
									far.active=false;}
								else{
									far.startnew=near.endnew;
								}
							}else if(far.endnew>near.startnew && far.endnew<=near.endnew){
								if(far.startnew>=near.startnew)far.active=false;else far.endnew=near.startnew;
							}else{
								LINE_INTERVAL li = new LINE_INTERVAL();
								li.start = near.startnew;
								li.end = near.endnew;
								far.intervals.Add(li);
							}Debug.Log(far.active);
						}//else{Debug.Log("WTF");}
					}
				}
			}
		}
	}
	
	private float line_cut(float l,float s, float e, float alfa, float beta){
		if(s<e){float t=s;s=e;e=t;beta=alfa-beta;}
		float sin_gama	= Max(Min(e*Mathf.Sin(alfa)/l,1.0f),0.0f);
		float gama		= Mathf.Asin(sin_gama);
		if(e>=s){
			return s*sin_gama/Mathf.Sin(gama-beta);
		}else{
			return s*sin_gama/Mathf.Sin(pi-gama-beta);
		}
	}
	#endregion
	
	#region CUT TO RADIUS
	
	#endregion
	
	#region EXPAND TO RADIUS
	
	#endregion
	
	//________________	
	#region Utility Functions
	
	private	float NormalAngle(Vector3 vertex){
		return Mathf.Atan2(vertex.z,vertex.x);
	}
	
	private	float LENGTH_(Vector3 a){		return Mathf.Sqrt(a.x*a.x+a.z*a.z);}
	
	//
	private	float PseudoAngle(Vector3 a, float distance){
		return -a.x/distance;
	}
		
	private	float Lenght(Vector3 a){return Mathf.Sqrt(a.x*a.x+a.z*a.z);}
		
	/*private	bool line_circle_intersection(Vector2 *c,float *r,Vector2 *p1,Vector2 *p2,Vector2 *closest1,Vector2 *closest2){
		const Vector2 dp = *p2-*p1;
		const Vector2 cp = *p1-*c;
		const float a = dp.Dot(&dp);
		const float b = 2.0f * dp.Dot(&cp);
		const float cc = c->Dot(c)+p1->Dot(p1)-2.0f*c->Dot(p1)-*r**r;
		const float bb4ac  = b * b - 4.0f * a * cc; 
		if(bb4ac < 0.0f)return false;
		const float powbb4ac=SQRT_(bb4ac);
		const float a2=0.5f/a;
		const float mu1 = (-b + powbb4ac)*a2;
		*closest1 = *p1 + dp*mu1;
		const float mu2 = (-b - powbb4ac)*a2;
		*closest2 = *p1 + dp*mu2;
		return true;
	}*/
		
	private	static float Min(float a, float b){
		//if(a<b){	return a;	}
		//return b;
		return (a<b) ? a : b;
	}
	
	private	static float Max(float a, float b){
		if(a<b){	return b;	}
		return a;
	}
	

	//NOT NEEDED YET, CONSIDER REMOVAL: JUST USE: Renderer.isVisible maybe
	//↘EDIT: no if 2 Cameras! we need to have area since objects between 2 cameras could get ignored
	
	//gather relevant Polygons, check the polygon position increased by its radius if it is near our relevant radius
	//	↘viewport may be rotateable, for convenience we check if in radius not if within a maybe rotated rectangular area
	private	void GatherPolygons(ref BottomPolygon[] polys, Vector3 position, float radius){
		List<BottomPolygon> relevantPolygons = new List<BottomPolygon>();
		for(int iP =0; iP<polys.Length;iP++){
			//Vector3.magnitude or Distance is slower than sqrMag
			if( ((polys[iP].position-position).sqrMagnitude - polys[iP].relevantRadius*polys[iP].relevantRadius) < radius*radius){
			//if( Vector3.Distance(polys[i].position, position) < radius){
				//relevantPolygons.Add(ref polys[iP]);
				relevantPolygons.Add(polys[iP]);
			}
		}
	}
	
	#endregion
	
	#region ANGLE VISUALIZATION
	
	private bool avEnabled		= false;
	private bool avSegments		= false;
	private bool avSegmentsNew	= false;
	private bool avSegmentLines	= false;
	
	private bool avHelperLines	=  true;
	private bool avLabels		=  true;
	
	private bool avInitiated	= false;
	List<GameObject> angleCube	= new List<GameObject>();
	
	//Labels
	List<GameObject> labelStart	= new List<GameObject>();
	List<GameObject> labelEnd	= new List<GameObject>();
	
	//Object parents
	GameObject cubeParent;
	GameObject labelParent;
	
	//Boarder cubes
	private GameObject leftBorder;
	private GameObject rightBorder;
	private GameObject bottomBorder;
	//Border Labels
	private GameObject[] borderlabels;
	
	//Diagram Scale
	private float avScaleX		= 100F;				//pseudoangle scale (±1)
	private float avScaleY		= 0.4F;
	private float avZOff		= -50F;
	
	//Drawing an occlusion Diagram out of colored cubes
	private void VisualizeAngles(){
		
		//create 200 empty cubes for usage, if they were to be created and destroyed every frame we have a memory dump
		if(!avInitiated){
			int maxCubes = 200;
			
			cubeParent = new GameObject();
			cubeParent.name = "OcclusionDiagram";
			
			labelParent = new GameObject();
			labelParent.name = "Labels";
			
			borderlabels = new GameObject[9];
			for(int i = 0; i< 9; i++){
				borderlabels[i] = CreateAVLabel(6000+i);	//create all labels for the diagram
				borderlabels[i].transform.parent = labelParent.transform;
				borderlabels[i].GetComponent<TextMesh>().anchor = TextAnchor.UpperRight;
			}
				
			leftBorder = CreateAVCube(00);
			rightBorder = CreateAVCube(00);
			bottomBorder = CreateAVCube(00);
			
			for(int i = 0; i < maxCubes; i++){
				
				//creation
				angleCube.Add(CreateAVCube(i));
				
				labelStart.Add(CreateAVLabel(i+1000));
				labelEnd.Add(CreateAVLabel(i+2000));
				labelStart[i].SetActive(false);
				labelEnd[i].SetActive(false);
				labelStart[i].transform.parent = labelParent.transform;
				labelEnd[i].transform.parent = labelParent.transform;
				labelStart[i].GetComponent<TextMesh>().anchor = TextAnchor.UpperRight;
				labelEnd[i].GetComponent<TextMesh>().anchor = TextAnchor.LowerLeft;
			}
			
			avInitiated = true;
			UpdateAVBorders();
		}
				
		if(avEnabled){
					
			//List<Segment> segments;
			for(int ic = 0; ic < angleCube.Count; ic++){
				if(avSegments || avSegmentsNew || avSegmentLines){
					if(!avSegmentLines && ic<=segments.Count-1 || avSegmentLines && ic<=segmentLines.Count-1){
										
						float length	= 0F;
						float height	= 0F;
								
						float x			= 0F;
						float y			= 0F;
						
						if(!avSegmentLines){											
							if(avSegments){				//use Segments
								length	= segments[ic].end - segments[ic].start;
								x		= segments[ic].start + length/2F;								
							}else if(avSegmentsNew){	//use Segments
								length	= segments[ic].endnew - segments[ic].startnew;
								x		= segments[ic].startnew + length/2F;								
							}
							
							height	= segments[ic].segmax - segments[ic].segmin;
							y		= segments[ic].segmin + height/2F;
														
							ManipulateVisualizationCube	( angleCube[ic], x * avScaleX, (y*avScaleY) +avZOff, length * avScaleX, height*avScaleY, segments[ic].active, ic);
							if(avHelperLines){DrawHelperLines( (x-length/2F) * avScaleX,(y-height/2F)*avScaleY +avZOff,(x+length/2F) * avScaleX);}
							
							labelStart[ic]	.GetComponent<TextMesh>().text = ""+segments[ic].start.ToString("F2");
							labelEnd[ic]	.GetComponent<TextMesh>().text = ""+segments[ic].end.ToString("F2");
							
						}else{	//use SegmentLines
							length	= segmentLines[ic].endnew - segmentLines[ic].startnew;
							x		= segmentLines[ic].startnew + length/2F;
							height	= segmentLines[ic].max - segmentLines[ic].min;
							y		= segmentLines[ic].min + height/2F;
							
							float avScaleXn		= avScaleX/(pi/2F);	//normalangle scale
							
							//angleCube[ic].SetActive(true);
							ManipulateVisualizationCube	( angleCube[ic], (x -pi/2) * avScaleXn, (y*avScaleY) +avZOff, length * avScaleXn, height*avScaleY, segmentLines[ic].active, ic);
							if(avHelperLines){DrawHelperLines( ((x-length/2F) -pi/2) * avScaleXn,(y-height/2F)*avScaleY +avZOff,((x+length/2F) -pi/2) * avScaleXn);}
							
							labelStart[ic]	.GetComponent<TextMesh>().text = ""+(segmentLines[ic].start*57.2957795F).ToString("F0");
							labelEnd[ic]	.GetComponent<TextMesh>().text = ""+(segmentLines[ic].end*57.2957795F).ToString("F0");
						}
					}else{	//disable/unrender unused objects
						HideVisualizationCube		( ic );
					}
				}else{	//cleanup, disable Visualization
					HideVisualizationCube		( ic );
										
					avEnabled = false;
					UpdateAVBorders();
				}
			}
		}
		
	}
	
	private float avHeight		=  10F;
	private float avThickness	=   1F;
	
	private void ManipulateVisualizationCube(GameObject cube, float x, float y, float length, float height, bool active, int labelI, Color? color = null){
		
		//color
		if(active){
			cube.renderer.material.color = new Color(0F,1F,0F,0.9F);
		}else{
			cube.renderer.material.color = new Color(1F,0F,0F,0.9F);
		}
		
		if(color != null){
			cube.renderer.material.color = (Color)color;
		}
		
		//manipulation
		cube.transform.localScale = new Vector3(length,avThickness,height);
		cube.transform.position = new Vector3(x,avHeight,y);
		
		//labels, if needed
		if(labelI != -1){
			if(avLabels){
				labelStart[labelI]	.SetActive(true);
				labelEnd[labelI]	.SetActive(true);
				labelStart[labelI]	.transform.position	= new Vector3(x-length/2F, avHeight+avThickness/2F, y-height/2F);
				labelEnd[labelI]	.transform.position	= new Vector3(x+length/2F, avHeight+avThickness/2F, y-height/2F);
			}else{
				labelStart[labelI]	.SetActive(false);
				labelEnd[labelI]	.SetActive(false);
			}
		}
		
		//return cube;
	}
	
	private void HideVisualizationCube(int i){
		angleCube[i]	.renderer.material.color = Color.clear;
		labelStart[i]	.SetActive(false);
		labelEnd[i]		.SetActive(false);
	}
	
	public Material avFontMat;
	public Font avFont;
	private GameObject CreateAVLabel(int i){
		GameObject textLabel = new GameObject();
			textLabel.name = "Label"+i;
			textLabel.transform.rotation = Quaternion.Euler(90F,0F,0F);
		
			textLabel.AddComponent<TextMesh>();
			textLabel.AddComponent<MeshRenderer>();
		
			TextMesh tm = textLabel.GetComponent<TextMesh>();
			tm.text = "EMPTY";
			tm.font = avFont;
			textLabel.GetComponent<MeshRenderer>().material = avFontMat;
		return textLabel;
	}
	
	private GameObject CreateAVCube(int i){
		
		GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
				
		Destroy(cube.GetComponent<BoxCollider>());//cube.collider.enabled = false;
		cube.GetComponent<MeshRenderer>().material = prismMat;
		cube.transform.parent = cubeParent.transform;
		cube.name = "Cube"+i;
		
		return cube;
	}
	
	private void DrawHelperLines(float x1, float y1, float x2){
		float y = avHeight+avThickness/2F;
		Color color = new Color(1F,1F,1F,0.2F);
		Debug.DrawLine(new Vector3(x1,y,y1), new Vector3(x1,y,100), color, 0F, false);
		Debug.DrawLine(new Vector3(x2,y,y1), new Vector3(x2,y,100), color, 0F, false);
	}
	
	private string[] pseudoDiagramLabels = new string[9]	{"Distance", "0", "↑", "Pseudoangle", "-1", "-0.5",   "0",  "0.5",    "1"};
	private string[] normalDiagramLabels = new string[9]	{"Distance", "0", "↑", "Angle",       "0°",  "45°", "90°", "135°", "180°"};
	
	private void UpdateAVBorders(){
		if(!avInitiated){return;}	
				
		//BORDERS
		if(avEnabled){
			Color left		= new Color(1F,0.5F,0.5F,0.8F);
			Color right		= new Color(0.5F,0.5F,1F,0.8F);
			Color bottom	= new Color(1F,1F,1F,0.8F);
			
			//														 X				  Z						L		  H
			ManipulateVisualizationCube(bottomBorder,	 		   0.0F	,		   -0.5F+avZOff , avScaleX *2F ,	  1F ,true, -1, bottom);//bottom
			ManipulateVisualizationCube(rightBorder,	-(avScaleX+0.5F), avScaleX/2F-1F+avZOff ,			1F ,avScaleX ,true, -1, left);//left
			ManipulateVisualizationCube(leftBorder,	 	 (avScaleX+0.5F), avScaleX/2F-1F+avZOff ,			1F ,avScaleX ,true, -1, right);//right
			
			borderlabels[0].transform.position = new Vector3(-avScaleX -3F,	avHeight, avZOff + avScaleX/2 );
			borderlabels[0].transform.rotation = Quaternion.Euler(90F,270F,0F);
			borderlabels[1].transform.position = new Vector3(-avScaleX -3F,	avHeight, avZOff );
			borderlabels[2].transform.position = new Vector3(-avScaleX -3F,	avHeight, avZOff + avScaleX );
			
			borderlabels[3].transform.position = new Vector3( 0,			avHeight,avZOff -3F );
			borderlabels[4].transform.position = new Vector3(-avScaleX,		avHeight,avZOff -1.5F );
			borderlabels[5].transform.position = new Vector3(-avScaleX/2,	avHeight,avZOff -1.5F );
			borderlabels[6].transform.position = new Vector3( 0,			avHeight,avZOff -1.5F );
			borderlabels[7].transform.position = new Vector3( avScaleX/2,	avHeight,avZOff -1.5F );
			borderlabels[8].transform.position = new Vector3( avScaleX,		avHeight,avZOff -1.5F );
			
		}else{
			leftBorder	.renderer.material.color = Color.clear;
			rightBorder	.renderer.material.color = Color.clear;
			bottomBorder.renderer.material.color = Color.clear;
		}
				
		for(int i = 0; i< 9; i++){			
			//if(avLabels && avEnabled){
			if(avEnabled){
				borderlabels[i].SetActive(true);
				if(avSegmentLines){
					borderlabels[i].GetComponent<TextMesh>().text = normalDiagramLabels[i];
				}else{
					borderlabels[i].GetComponent<TextMesh>().text = pseudoDiagramLabels[i];
				}
			}else{
				borderlabels[i].SetActive(false);
			}
		}
	}
	
	#endregion
	
	#region VISIBILITY VERTEX LABELS
	
	//draws index labels onto points
	private List<GameObject> labelVis	= new List<GameObject>();
	private bool vlInitiated = false;
	//private bool vlLabels = true;
	public	void VisibilityLabels(ref List<Vector3> output){
		//Debug.Log(output.Count-1);
		if(!vlInitiated){
			int maxLabels = 200;
			labelParent = new GameObject();
			labelParent.name = "VertexLabels";
						
			for(int i = 0; i < maxLabels; i++){
				//creation
				labelVis.Add(CreateAVLabel(i+4000));
				labelVis[i].SetActive(false);
				labelVis[i].transform.parent = labelParent.transform;
			}
			
			vlInitiated = true;
		}
		
		//int count = (output.Count < labelVis.Count)? output.Count : labelVis.Count;		
		for(int ic = 0; ic < labelVis.Count; ic++){						
			if(ic < output.Count){
				labelVis[ic]	.SetActive(true);
				labelVis[ic]	.transform.localScale	= new Vector3(0.2F,0.2F,0.2F);
				labelVis[ic]	.transform.position		= new Vector3(output[ic].x, 0F, output[ic].z);
				if(ic%2==1){					
					labelVis[ic].GetComponent<TextMesh>().text = "-"+ic;
					labelVis[ic].GetComponent<TextMesh>().anchor = TextAnchor.MiddleLeft;
				}else{
					labelVis[ic].GetComponent<TextMesh>().text = ic+"-";
					labelVis[ic].GetComponent<TextMesh>().anchor = TextAnchor.MiddleRight;
				}
			}else{
				labelVis[ic]	.SetActive(false);
			}
		}
	}
	
	
	#endregion
	
	#region	VISIBILITY POLYGON GENERATION
	private	void GenerateVisibilityVertices(){//VIEWSYSTEM::finish_system(){
			//sort_segments();
			//segment_drop();
			//segment_convert_to_lines();
			//sort_lines();
			//cut_lines();
			//sort_lines();
			//line_drop();
			//line_recalc();
			//sort_lines();
			//crop_radius();
			//expand_to_radius();
	}
	
	#endregion	
	
	#region	Camera & Fader
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
	
	#region GUI bools
	
	//GUI Utility
	//DRAW
	private	bool drawLineToVertices	= false;
	private	bool drawPolygons		= true;
	private	bool drawVisibleFaces	= false;
	private	bool drawSegments		= false;
	private	bool drawLines			= false;
	private	bool drawCuts			= false;
	private	bool drawRecalc			= false;
	private	bool drawFinal			= false;
		
	//GENERATE
	private	bool checkCCW			= true;
	private	bool worldNormals		= false;
	private	bool invisibleFaces		= false;
	private	bool autoGen			= false;
	private	bool clampY				= false;
	
	//VISIBILITY
	private	bool gatherSegments		= true;
	private	bool gatherTop			= false;
	private	bool gatherBottom		= false;
	
	private	bool extrude			= false;
	private	bool invert				= true;
	
	//start of converted serumas functions
	private	bool dropSegments		= false;
	private	bool convertLines		= true;
	private bool cutLines			= false;
	private bool dropLines			= false;
	
	//STUFF
	private	bool fadeTest			= true;
	private	bool vsyncOn			= false;
	
	private	bool showSegStats		= false;
	private	bool showVertOrder		= false;
	
	private	int  polyCount			= 0;
	private	int  frameRate			= 0;
	
	private bool cameraTop			= false;
	
	public	string lastTooltip = " ";
	private	bool mouseOver = false;
	
	#endregion
		
	#region GUI
	
	private int topOff = 0;		//scrolling through GUI
	private int guisize = 944; //total size of the GUI (buttons on left) in pixels
	private int linecount2ndApp = 0;
	private	void OnGUI(){
		
		
		//Slider
		if			(Input.mousePosition.y > Screen.height-25 && Input.mousePosition.x < 310){
			topOff-=3;
		}else if	(Input.mousePosition.y < 25 && Input.mousePosition.x < 310){
			topOff+=3;
		}			
		topOff = (int)GUI.VerticalScrollbar(new Rect(5, 15, 15, Screen.height-30), topOff, Screen.height+Screen.height-guisize, 0F, Screen.height-30);
				
		int	bh	= 22;
		int	y	= 10;
		int	x	= 25;
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
			"ENABLE GIZMOS!\n" +
			"\n" +
			"Segments:"+segmentCount+
			"Lines:"+linecount2ndApp);
		
		
		//debug of SegmenGeneration
		if(showSegStats){
			GUI.Label (new Rect(Screen.width-325,y+120,800,bh),("TOTAL SEGMENTS:\t\t"+segmentCount));
			GUI.Label (new Rect(Screen.width-200,y+140,800,bh*20),segmentValues[0]);
			GUI.Label (new Rect(Screen.width-400,y+140,800,bh*20),segmentValues[1]);
		}
		
		y	= y-topOff;
		
		GUI.Label (new Rect(x,y,600,bh),"Create Random Polygon ["+polyCount+"]"); y+=dy;
		if(GUI.Button (new Rect(x,	y, bl/2, bh),	new GUIContent("Create",	"cr")))	{CreateRandomPolygon(ref randomPolys);}
		if(GUI.Button (new Rect(x+bl/2,y, bl/2, bh),	new GUIContent("Clear",	"cl")))	{ClearRandomPolygons(ref randomPolys);}y+=dy;
		if(GUI.Button (new Rect(x,	y, bl, bh),	new GUIContent("Auto",		"13")))	{autoGen = !autoGen;}
		GUI.enabled = false;
		autoGen	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	autoGen, ""));y+=dy;
		GUI.enabled = true;
		y+=10;
		GUI.Label (new Rect(x,y,600,bh),"Create Random Prism ["+prismcount+"]"); y+=dy;
		if(GUI.Button (new Rect(x,	y, bl, bh),	new GUIContent("CreateRandomPrism",	"99")))	{CreateBottomPlane(true); Refresh();}y+=dy;
		
		GUI.Label (new Rect(x+150,y-4*dy-10,400,bh),"VisibilityPoly Options:");
		if(GUI.Button (new Rect(x+150,	y-3*dy-10, bl, bh),	new GUIContent("Extrude "+extrude,"1ad")))	{extrude = !extrude;}y+=dy;
		if(GUI.Button (new Rect(x+150,	y-3*dy-10, bl, bh),	new GUIContent("Invert "+invert,"1ad")))	{invert = !invert;}y+=dy;
		
	y+=dy2;
		
	//DRAW
		GUI.Label (new Rect(x,y,400,bh),"Draw:"); y+=dy;					//   ↓ needed to check if hovering
		if(GUI.Button (new Rect(x,	y, bl, bh),	new GUIContent("LineToVertices","1")))	{drawLineToVertices = !drawLineToVertices;}y+=dy;
		if(GUI.Button (new Rect(x,	y, bl, bh),	new GUIContent("Polygons",		"2")))	{drawPolygons = !drawPolygons;}y+=dy;
		if(GUI.Button (new Rect(x,	y, bl, bh),	new GUIContent("VisibleFacesTest",	"3")))	{drawVisibleFaces = !drawVisibleFaces;}y+=dy;
		if(GUI.Button (new Rect(x,	y, bl, bh),	new GUIContent("Segments",		"4")))	{drawSegments = !drawSegments;}y+=dy;
		if(GUI.Button (new Rect(x,	y, bl, bh),	new GUIContent("Lines",			"5")))	{drawLines = !drawLines;}y+=dy;
		GUI.enabled = false;
		if(GUI.Button (new Rect(x,	y, bl, bh),	new GUIContent("Cuts",			"6")))	{drawCuts = !drawCuts;}y+=dy;
		if(GUI.Button (new Rect(x,	y, bl, bh),	new GUIContent("Recalc",		"7")))	{drawRecalc = !drawRecalc;}y+=dy;
		if(GUI.Button (new Rect(x,	y, bl, bh),	new GUIContent("Final",			"8")))	{drawFinal = !drawFinal;}y+=dy;
		GUI.enabled = true;
		
		//indicators
		y-=dy*8;
		GUI.enabled = false;
		drawLineToVertices	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	drawLineToVertices, ""));	y+=dy;
		drawPolygons		= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	drawPolygons, ""));			y+=dy;
		drawVisibleFaces	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	drawVisibleFaces, ""));		y+=dy;
		drawSegments		= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	drawSegments, ""));			y+=dy;
		drawLines			= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	drawLines, ""));			y+=dy;
		drawCuts			= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	drawCuts, ""));				y+=dy;
		drawRecalc			= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	drawRecalc, ""));			y+=dy;
		drawFinal			= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	drawFinal, ""));			y+=dy;
		GUI.enabled = true;
		
		y-=dy*9;
		
	//Visibility
		GUI.Label (new Rect(x+150,y,400,bh),"Calculation:"); y+=dy;
		if(GUI.Button (new Rect(x+150,	y, bl, bh),	new GUIContent("Gather Segments","1a")))	{gatherSegments = !gatherSegments;}y+=dy;
			if(GUI.Button (new Rect(x+170,	y, bl-20, bh),	new GUIContent("↘TopHalf",			"4")))	{gatherTop = !gatherTop;}y+=dy;
			if(GUI.Button (new Rect(x+170,	y, bl-20, bh),	new GUIContent("↘BottomHalf",		"4h")))	{gatherBottom = !gatherBottom;}y+=dy;
		if(GUI.Button (new Rect(x+150,	y, bl, bh),	new GUIContent("Drop Segments",	"2a")))	{dropSegments = !dropSegments;}y+=dy;
		if(GUI.Button (new Rect(x+150,	y, bl, bh),	new GUIContent("Convert Lines",	"3a")))	{convertLines = !convertLines;}y+=dy;
		if(GUI.Button (new Rect(x+150,	y, bl, bh),	new GUIContent("Cut Lines",	"4a")))	{cutLines = !cutLines;}y+=dy;
		if(GUI.Button (new Rect(x+150,	y, bl, bh),	new GUIContent("Drop Lines",	"5a")))	{dropLines = !dropLines;}y+=dy;
		GUI.enabled = false;
		if(GUI.Button (new Rect(x+150,	y, bl, bh),	new GUIContent("Crop Radius",	"6a")))	{drawFinal = !drawFinal;}y+=dy;
		if(GUI.Button (new Rect(x+150,	y, bl, bh),	new GUIContent("Expand Radius",	"7a")))	{drawFinal = !drawFinal;}y+=dy;
		if(GUI.Button (new Rect(x+150,	y, bl, bh),	new GUIContent("",	"8a")))	{drawFinal = !drawFinal;}y+=dy;
		GUI.enabled = true;
		
		//indicators
		y-=dy*10;
		GUI.enabled = false;
		gatherSegments		= (GUI.Toggle (new Rect(x+off+150,y, 70, bh),	gatherSegments, ""));	y+=dy;
			gatherTop		= (GUI.Toggle (new Rect(x+off+150,y, 70, bh),	gatherTop, ""));		y+=dy;
			gatherBottom	= (GUI.Toggle (new Rect(x+off+150,y, 70, bh),	gatherBottom, ""));		y+=dy;
		dropSegments		= (GUI.Toggle (new Rect(x+off+150,y, 70, bh),	dropSegments, ""));		y+=dy;
		convertLines		= (GUI.Toggle (new Rect(x+off+150,y, 70, bh),	convertLines, ""));		y+=dy;
		cutLines			= (GUI.Toggle (new Rect(x+off+150,y, 70, bh),	cutLines, ""));			y+=dy;
		dropLines			= (GUI.Toggle (new Rect(x+off+150,y, 70, bh),	dropLines, ""));		y+=dy;
		drawFinal			= (GUI.Toggle (new Rect(x+off+150,y, 70, bh),	drawFinal, ""));		y+=dy;
		drawFinal			= (GUI.Toggle (new Rect(x+off+150,y, 70, bh),	drawFinal, ""));		y+=dy;
		drawFinal			= (GUI.Toggle (new Rect(x+off+150,y, 70, bh),	drawFinal, ""));		y+=dy;
		GUI.enabled = true;
	
	y+=dy2;
	//Visualization of Segments and line representation avSegments avSegmentLines
		GUI.Label (new Rect(x,y,400,bh),"Occlusion Diagram:"); y+=dy;
		bool avChanged = avEnabled;	//check if av has been enabled, used to keep non topview if it has been selected after AV has been enabled
		if(GUI.Button (new Rect(x,	y, bl, bh),	new GUIContent("Segments","1"))){
			avSegments = !avSegments;
			if(avSegments){avSegmentsNew = avSegmentLines = false; avEnabled = true;}
			UpdateAVBorders();
		}y+=dy;
																							
		if(GUI.Button (new Rect(x,	y, bl, bh),	new GUIContent("Segments CutAngles","2"))){
			avSegmentsNew = !avSegmentsNew;
			if(avSegmentsNew){avSegments = avSegmentLines = false; avEnabled = true;}
			UpdateAVBorders();
		}y+=dy;
		
		if(GUI.Button (new Rect(x,	y, bl, bh),	new GUIContent("Segmentlines",	"3")))	{
			avSegmentLines = !avSegmentLines;
			if(avSegmentLines){avSegments = avSegmentsNew = false; avEnabled = true;}
			UpdateAVBorders();
		}y+=dy+5;
		
		GUI.enabled = false;
		if(avEnabled){GUI.enabled = true;}
		if(GUI.Button (new Rect(x+20,	y, bl-20, bh),	new GUIContent("↘Helper Lines",	"4")))	{
			avHelperLines = !avHelperLines;
			UpdateAVBorders();
		}y+=dy;
		
		GUI.enabled = false;
		if(avEnabled){GUI.enabled = true;}
		if(GUI.Button (new Rect(x+20,	y, bl-20, bh),	new GUIContent("↘Labels",	"4")))	{
			avLabels = !avLabels;
			UpdateAVBorders();
		}y+=dy;
		
		if(avChanged != avEnabled){
			cameraTop = true;
			ToggleTopView();
		}
		
		y-=dy*5+5;
		
		//indicators
		GUI.enabled = false;
		avSegments			= (GUI.Toggle (new Rect(x+off,y, 70, bh),	avSegments, ""));		y+=dy;
		avSegmentsNew		= (GUI.Toggle (new Rect(x+off,y, 70, bh),	avSegmentsNew, ""));	y+=dy;
		avSegmentLines		= (GUI.Toggle (new Rect(x+off,y, 70, bh),	avSegmentLines, ""));	y+=dy+5;
		avHelperLines		= (GUI.Toggle (new Rect(x+off,y, 70, bh),	avHelperLines, ""));	y+=dy;
		avLabels			= (GUI.Toggle (new Rect(x+off,y, 70, bh),	avLabels, ""));			y+=dy;
		GUI.enabled = true;
		
	y+=dy2;		
	//Polygon Extraction/Generation
		GUI.Label (new Rect(x,y,400,bh),"Polygon Extraction/Generation:"); y+=dy;
		
		if(GUI.Button (new Rect(x,	y, bl, bh),	new GUIContent("Refresh",		"9")))	{
			Refresh();
		}
		y+=dy;
		if(GUI.Button (new Rect(x+20,	y, bl-20, bh),	new GUIContent("↘checkCCW",		"10")))	{checkCCW = !checkCCW;}
		checkCCW	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	checkCCW, ""));	y+=dy;
		
		if(GUI.Button (new Rect(x+20,	y, bl-20, bh),	new GUIContent("↘worldNormals",		"11")))	{worldNormals = !worldNormals;}
		worldNormals	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	worldNormals, ""));	y+=dy;
					
		if(GUI.Button (new Rect(x+20,	y, bl-20, bh),	new GUIContent("↘clampY to 0",		"12")))	{clampY = !clampY;}
		clampY	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	clampY, ""));	y+=dy;
				
	y+=dy2;
	//Other Options
		GUI.Label (new Rect(x,y,400,bh),"Other Stuff:"); y+=dy;
		
		if(GUI.Button (new Rect(x,	y, bl, bh),	new GUIContent("VSync",		"98")))	{ToggleVsync();}
		vsyncOn	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	vsyncOn, ""));		
		GUI.skin.label .fontSize = 16;
		GUI.Label (new Rect(x+off+25,	y-2, 120, bh*6),"FPS:"+frameRate);
		GUI.skin.label .fontSize = 12;
		y+=dy;
		
		if(GUI.Button (new Rect(x,	y, bl, bh),	new GUIContent("Top View",		"95")))	{cameraTop = !cameraTop;
			ToggleTopView();
		}
		cameraTop	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	cameraTop, ""));	y+=dy;
			
		if(GUI.Button (new Rect(x,	y, bl, bh),	new GUIContent("TestFader",		"96")))	{fadeTest = !fadeTest; FaderTest(ref staticBlockers);}
		fadeTest	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	fadeTest, ""));	y+=dy;
				
		if(GUI.Button (new Rect(x,	y, bl, bh),	new GUIContent("SingleSegmentStats",	"97")))	{showSegStats = !showSegStats;}
		showSegStats	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	showSegStats, ""));	y+=dy;
		
		if(GUI.Button (new Rect(x,	y, bl, bh),	new GUIContent("ShowVertexOrder",	"98")))	{
			showVertOrder = !showVertOrder;
			if(showVertOrder){StartCoroutine("VertOder");}else{StopCoroutine("VertOder");};
		}
		showVertOrder	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	showVertOrder, ""));	y+=dy;
						
	y+=dy2;		
	//Visibility Polygon Options
		GUI.Label (new Rect(x,y,400,bh),"Visibility Polygon Options:"); y+=dy;
		
		if(GUI.Button (new Rect(x,	y, bl, bh),	new GUIContent("InvisibleFaces",		"13")))	{invisibleFaces = !invisibleFaces;}
		invisibleFaces	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	invisibleFaces, ""));	y+=dy;
		
		//Future:
		bool emptybool = false;
		GUI.enabled = false;
		if(GUI.Button (new Rect(x,	y, bl, bh),	new GUIContent("Extrude",		"14")))	{}
		emptybool	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	emptybool, ""));	y+=dy;
		
		if(GUI.Button (new Rect(x+20,	y, bl-20, bh),	new GUIContent("↘extrusion height",		"15")))	{emptybool = !emptybool;}
		emptybool	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	emptybool, ""));	y+=dy;
		
		if(GUI.Button (new Rect(x,	y, bl, bh),	new GUIContent("Inverted",		"16")))	{emptybool = !emptybool;}
		emptybool	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	emptybool, ""));	y+=dy;
		
		if(GUI.Button (new Rect(x+20,	y, bl-20, bh),	new GUIContent("↘inverted Radius",		"17")))	{emptybool = !emptybool;}
		emptybool	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	emptybool, ""));	y+=dy;
		
		if(GUI.Button (new Rect(x,	y, bl, bh),	new GUIContent("Transparency",		"18")))	{emptybool = !emptybool;}
		emptybool	= (GUI.Toggle (new Rect(x+off,	y, 70, bh),	emptybool, ""));	y+=dy;
		
		GUI.enabled = true;
		
		
		//Mouse above GUI check
		if (Event.current.type == EventType.Repaint && GUI.tooltip != lastTooltip) {
			if (lastTooltip != "")
				SendMessage("OnMouseOut", SendMessageOptions.DontRequireReceiver);
			
			if (GUI.tooltip != "")
				SendMessage("OnMouseOver", SendMessageOptions.DontRequireReceiver);
			lastTooltip = GUI.tooltip;
		}
		
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
	
	//shows vertex order
	private	IEnumerator VertOder(){
		if(randomPolys.Length > 0){	//try to show vertices on random poly
			for(int i = 0; i<randomPolys[0].vertices.Length; i++){
				Debug.DrawLine(source.position, randomPolys[0].vertices[i],Color.green,0.25F,false);
				yield return new WaitForSeconds(0.24F);
			}
			showVertOrder = false;
			StopCoroutine("VertOder");
		}else if(staticBlockers.Length > 0){	//if no random poly is present try static blockers
			for(int i = 0; i<staticBlockers[0].vertices.Length; i++){
				Debug.DrawLine(source.position, staticBlockers[0].vertices[i],Color.green,0.25F,false);
				yield return new WaitForSeconds(0.24F);
			}
			showVertOrder = false;
			StopCoroutine("VertOder");
		}else{
			showVertOrder = false;
			StopCoroutine("VertOder");
		}
	}
	
	private	void OnMouseOver()	{ mouseOver	=  true;}
	private	void OnMouseOut()	{ mouseOver	= false;}
	
	#endregion
	
	#region PRISMATRANSFORMATION
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
		
			verticesT = vertices;
			trianglesT = triangles;
			StopCoroutine("ShowTriangles");
			//StartCoroutine("ShowTriangles",pos);
							
		//Convert to prism
			if(random)
			ConvertPlaneToPrism(ref newGO, pos, true);
		}
	
	
		//it would be much cheaper if we directly generate the plane extruded
		private	void ConvertPlaneToPrism(ref GameObject plane, Vector3? origin = null, bool? showTriangles = null){
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
				
			if(showTriangles != null){
			
				verticesT = vertices;
				trianglesT = triangles;
				StartCoroutine("ShowTriangles",origin);
		
				meshR.material = prismMat;
			
				//focus new prism
				lerpCam		= true;
				lerpTime	= Time.time;
				if(origin != null){targetPosition	= (Vector3)origin;}
			}
			
		}
	
	
		Vector3[]	verticesT;
		int[]		trianglesT;
		private	IEnumerator ShowTriangles(Vector3 p){
			//showTriangles
			Color colora = new Color(0F,1F,0.5F,0.5F);
			Color colorb = new Color(0F,0.5F,1F,0.5F);
			Color color = colora;
			float time1 = 0.5F;
			float time2 = 0.2F;
			for(int i = 0; i < trianglesT.Length;){							
				Debug.DrawLine(verticesT[trianglesT[i+0]]+p,verticesT[trianglesT[(i+1)%trianglesT.Length]]+p,color,time1,false);
				Debug.DrawLine(verticesT[trianglesT[i+1]]+p,verticesT[trianglesT[(i+2)%trianglesT.Length]]+p,color,time1,false);
				Debug.DrawLine(verticesT[trianglesT[i+2]]+p,verticesT[trianglesT[(i+0)%trianglesT.Length]]+p,color,time1,false);
				//Debug.Log("v1["+trianglesT[i+0]+"]\tv2["+trianglesT[i+1]+"]\tv3["+trianglesT[i+2]+"]");
				i+=3;
				if(i%2==1){color = colora;}else{color = colorb;}
				yield return new WaitForSeconds(time2);
			}
		}
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
						
				//generation of the BottomPlane with Mesh
				vertices[vertCount-1] = pos; //center is stored at the end
				int iT = 0;	//triangle Iterator
				for(int i = 0; i<vertCount-1; i++){	//dont iterate through center		
					vertices[i]	= output[i];
					colors[i]	= Color.white;
					uv[i]		= new Vector2(0F,0F);
					normals[i]	= Vector3.down;	//only down normals are gathered
					
					//triangles: modulo version
					/*if(i>0){//ignore first point(middle point)
						triangles[iT] = 0;						iT++;	//middle
						triangles[iT] = (i+1)%(vertCount);		iT++;	//next
						triangles[iT] = i;						iT++;	//current
					}
					//last vertex
					if(i== vertCount-1){
						//last triangle would add 0 twice
						triangles[iT-1] = 1;
					}*/
					
					//triangles: non modulo version
					if(i>0){
						triangles[iT] = vertCount-1;	iT++;	//middle
						triangles[iT] = i;				iT++;	//current
						triangles[iT] = i-1;			iT++;	//previous
					}else{
						triangles[iT] = vertCount-1;	iT++;	//middle
						triangles[iT] = 1;				iT++;	//current
						triangles[iT] = vertCount-2;	iT++;	//previous
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
				//visMesh.RecalculateNormals();
				//visMesh.RecalculateBounds();
				
				
			}else{//EXTRUDED (prism)
														
				//Create Mesh	
				
				int vertCount = output.Count+2;	//+center point + center point on top
				
				Vector3[]	vertices	= new Vector3	[vertCount*2];
				Color[]		colors		= new Color		[vertCount*2];
				Vector2[]	uv			= new Vector2	[vertCount*2];
				Vector3[]	normals		= new Vector3	[vertCount*2];
				
				int			tOff		= 				vertCount*3;		//	1/4 of the triangles count (1/4 bottom, 1/4 top, 1/2 outer side planes)
				int[]		triangles	= new int		[vertCount*3 *4];	//	*4 because off side Quads and Top triangles	
			
				
				//generate the mesh
				
				Vector3 up = new Vector3(0F,2.5F,0F);
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
					uv		[i]	= uv[i+vertCount]		= new Vector2(0F,0F);
					normals	[i]	= Vector3.down;
					normals	[i+vertCount] = Vector3.up;
					
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
				//visMesh.tangents
				visMesh.normals = normals;
				visMesh.triangles = triangles;
				
			
				//visMesh.RecalculateNormals();
				//visMesh.RecalculateBounds();	
			}
				
				
			
//INVERTED-STARSHAPED-VISIBILITY-POLY
		}else{
			if(!extrude){//NOT EXTRUDED (plane)
				int vertCount = output.Count;
				float perimeter = 300F;	//outer perimeter, should be larger than vieport diameter /2 when used ingame
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
				
				float perimeter = 300F;	//outer perimeter, should be larger than vieport diameter /2 when used ingame						
				
				//Create Mesh					
				int vertCount = output.Count;				
				Vector3[]	vertices	= new Vector3	[vertCount*4];
				Color[]		colors		= new Color		[vertCount*4];
				Vector2[]	uv			= new Vector2	[vertCount*4];
				Vector3[]	normals		= new Vector3	[vertCount*4];
				
				int			tOff		= 				vertCount*3;		//	1/4 of the triangles count (1/4 bottom, 1/4 top, 1/4 inner side planes, 1/2 outer side planes)
				int[]		triangles	= new int		[vertCount*3 *8];	//	*8 because off side Quads (4 triangles/output point) and Top/bottom Quads (another 4)	
			
				
				//generate the mesh				
				Vector3 up = new Vector3(0F,2.5F,0F);
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
					// cur.iteration -> next iteration, scrap top bottom every 2nd cycle, future..
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
				//visMesh.tangents
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

	private	bool postRender = false;
	private void OnPostRender(){
		if(postRender){
			CreateLineMaterial();
			lineMaterial.SetPass( 0 );
			GL.Begin( GL.LINES );
			//GL.Begin( GL.TRIANGLES );
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
			
			GL.Begin( GL.LINES );
			GL.Color( new Color(1F,1F,0F,0.5F) );
			for(int i = 0; i<postRenderOutput.Count; i+=2){
				GL.Vertex3( postRenderOutput[i  ].x, postRenderOutput[i  ].y, postRenderOutput[i  ].z);
				GL.Vertex3( postRenderOutput[i+1].x, postRenderOutput[i+1].y, postRenderOutput[i+1].z);
			}			
			GL.End();
		}
	}
	
	#endregion
	
}
