#define USE_CUBE_VERTEX
//#define USE_CUBE_CENTER
#define HIDE_WHEN_MISS

//#define USE_PREPROCESSED_IMAGE

using UnityEngine;
using System.Collections;
using System;
using AOT;
using System.Runtime.InteropServices;

using System.IO;

public class MarkerAR : MonoBehaviour {

	[StructLayout(LayoutKind.Sequential)]  
	struct Param {
		public int width;
		public int height;
		public float fx;
		public float fy;
		public float cx;
		public float cy;
	};

	[StructLayout(LayoutKind.Sequential)]  
	struct Result {
		public int id;
		public unsafe fixed float Position[3];
		public unsafe fixed float Rotation[9];
		public unsafe fixed double cubeVertex[24];
		public unsafe byte* img;
		//public float w,x,y,z;
	};

	delegate void callback(IntPtr param,IntPtr num); 


	[DllImport ("__Internal")]  
	unsafe static extern void initialise(Param *p);
	[DllImport ("__Internal")]  
	unsafe static extern void pushImage(char *data, callback result);
	[DllImport ("__Internal")]  
	unsafe static extern void pushImageUnity(char *data, callback result);
	[DllImport ("__Internal")]  
	unsafe static extern void pushMarker(char*data);
	[DllImport ("__Internal")]  
	unsafe static extern void finishPushMarker();  

	const int MAX_MARKER = 5;

	static WebCamTexture webcamTexture;
	static Color32[] colors;
	static int lastw=0,lasth=0,w=0,h=0;
	static bool showCube=false;
	static bool markerTransfered=false;
	static bool[] markerFirstShow = new bool[MAX_MARKER];
	static Vector3[,] cubeVertexs=new Vector3[MAX_MARKER,8];
	//static float[] SCALE_OFFSET=new float[MAX_MARKER]{2f,1.5f,0.5f,2f,2f};
	static float[] SCALE_OFFSET = new float[MAX_MARKER]{5.739f,27.11736f,10.27f,34.5113f,34.5113f};
	//static Vector3 EULER_ANGLE_OFFSET = new Vector3 (-270f, 90f, -90f);
	static Vector3 EULER_ANGLE_OFFSET = new Vector3 (90f, 180f, 0f);
	static Matrix4x4 ROTATION_OFFSET = new Matrix4x4 ();
	//Texture2D correctTex;

	public GameObject prefab; //this is a cube prefab
	public GameObject[] objectPrefab=new GameObject[MAX_MARKER];
	static GameObject[] objects_go = new GameObject[MAX_MARKER];
	static GameObject[] vertexs_go = new GameObject[8];

	float _updateInterval = 1f;//设定更新帧率的时间间隔为1秒  
	float _accum = .0f;//累积时间  
	int _frames = 0;//在_updateInterval时间内运行了多少帧  
	float _timeLeft;  

	// Use this for initialization
	void Start () 
	{

		_timeLeft = _updateInterval;  

		#if (USE_CUBE_VERTEX)
			for (int i = 0; i < 8; i++) {
				vertexs_go[i] = Instantiate (prefab);
				vertexs_go[i].name = "Cube" + i;

				Material m = vertexs_go[i].GetComponent<MeshRenderer> ().material;
				switch (i) {
				case 0:
					m.color = Color.red;
					break;
				case 1:
					m.color = Color.yellow;
					break;
				case 2:
					m.color = Color.green;
					break;
				case 3:
					m.color = Color.blue;
					break;
				case 4:
					m.color = Color.cyan;
					break;
				case 5:
					m.color = Color.gray;
					break;
				}
				vertexs_go[i].transform.position = new Vector3 (i - 4, i - 4, 10); 
				vertexs_go[i].GetComponent<Renderer>().enabled=false;
			}
		#endif
		#if (USE_CUBE_CENTER)
		for (int i=0;i<MAX_MARKER;i++)
		{
			objects_go[i] = Instantiate (prefab);
			objects_go[i].name="Cubes"+i;
			objects_go[i].transform.position=new Vector3(0,0,0);
			objects_go[i].GetComponent<Renderer>().enabled=false;
			Material m = objects_go[i].GetComponent<MeshRenderer> ().material;
			switch (i) {
			case 0:
				m.color = Color.red;
				break;
			case 1:
				m.color = Color.yellow;
				break;
			case 2:
				m.color = Color.green;
				break;
			case 3:
				m.color = Color.blue;
				break;
			case 4:
				m.color = Color.cyan;
				break;
			}
		}
		#else //使用模型
		for (int i=0;i<MAX_MARKER;i++)
		{
			objects_go[i] = Instantiate (objectPrefab[i]);
			objects_go[i].name="Objects"+i;
			//objects_go[i].transform.position = new Vector3 (i - 4, i - 4, 10); 
			//objects_go[i].transform.localScale=new Vector3(0.05f,0.05f,0.05f);
			objects_go[i].SetActive(false);
		}
		#endif

		for (int i=0;i<MAX_MARKER;i++)
			markerFirstShow[i]=true;

		WebCamDevice[] devices = WebCamTexture.devices;
		if (devices.Length > 0)
		{
			Debug.Log ("[DQC]:"+devices.Length+" devices");
			if (devices.Length == 1)
				Debug.Log ("[DQC]:" + devices [0].name);
			else if (devices.Length == 2)
				Debug.Log ("[DQC]:" + devices [0].name+"#"+devices[1].name);
			
			webcamTexture = new WebCamTexture(devices[0].name,640,480,30);		
			webcamTexture.Play();			

			GetComponent<Renderer> ().material.mainTexture = webcamTexture;

			}
		else
			Debug.Log ("[DQC]:no webcam devices found");


		ROTATION_OFFSET [0, 0] = -1;
		ROTATION_OFFSET [1, 2] = -1;
		ROTATION_OFFSET [2, 1] = -1;

		/*
		//export the array of pixels of markers
		for (int i=1;i<=4;i++)
		{
			Texture2D tex=(Texture2D)Resources.Load ("marker" + i);
			colors = tex.GetPixels32 ();
			FileStream fs = new FileStream ("marker" + i+".txt",FileMode.Create);
			FileStream fs2 = new FileStream ("markerx" + i+".txt",FileMode.Create);
			StreamWriter sw = new StreamWriter (fs);
			StreamWriter sw2 = new StreamWriter (fs2);
			unsafe {
				fixed(Color32* p=&colors[0]){
					byte* pp=(byte*)p;
					for (int k=0;k<colors.Length;k++)
						sw.WriteLine((*pp++)+" "+(*pp++)+" "+(*pp++)+" "+(*pp++));
				}
			}
			sw.Close();

			for (int k = 0; k < colors.Length; k++)
				sw2.WriteLine (colors [k].r + " " + colors [k].g + " " + colors [k].b + " " + colors [k].a);

			sw2.Close ();
		}
		*/


		/*
		//show the markers on the background
			Texture2D tex = (Texture2D)Resources.Load ("marker"+1);
			//GetComponent<Renderer> ().material.mainTexture = tex;

			colors=tex.GetPixels32();
			int newlength=(int)(colors.Length);
			Color32[] newcolors=new Color32[newlength];
			for (int i=0;i<(int)(300);i++)
				newcolors[i]=colors[i];
			Texture2D correctTex = new Texture2D (tex.width,tex.height);	
			correctTex.SetPixels32 (newcolors);
			correctTex.Apply ();
			GetComponent<Renderer> ().material.mainTexture = correctTex;
		*/
		}

		// Update is called once per frame
		void Update ()
		{
		
		if (WebCamTexture.devices.Length == 0)
			return;

		_timeLeft -= Time.deltaTime;  
		//Time.timeScale可以控制Update 和LateUpdate 的执行速度,  
		//Time.deltaTime是以秒计算，完成最后一帧的时间  
		//相除即可得到相应的一帧所用的时间  
		_accum += Time.timeScale / Time.deltaTime;  
		++_frames;//帧数  

		if (_timeLeft <= 0) {  
			float fps = _accum /_frames;  
			//Debug.Log(_accum + "__" + _frames);  
			Debug.Log("[FPS]"+fps.ToString()+"\n");
			_timeLeft = _updateInterval;  
			_accum = .0f;  
			_frames = 0;  
		}  


		w = webcamTexture.width;
		h = webcamTexture.height;

		if (w > 50 && h > 50) {
			if (w!=lastw || h!=lasth)
			{
				Debug.Log("[DQC]:new w:"+w+" new h:"+h);
				lastw=w;
				lasth=h;
				#if (USE_PREPROCESSED_IMAGE)
					cb_image=new Texture2D(w,h);
					colors=new Color32[w*h];
					GetComponent<Renderer> ().material.mainTexture=cb_image;
				#endif

				//transport markers
				if (!markerTransfered){
					
					markerTransfered=true;

					Param p;
					p.width=w;
					p.height=h;
					p.cx=w/2;
					p.cy=h/2;
					p.fx=535.5f;
					p.fy=535.5f;
					unsafe{
						initialise (&p);
					}


					Texture2D tex;

					for (int i=1;i<=4;i++)
					{
						tex = (Texture2D)Resources.Load ("marker"+i);
						colors = tex.GetPixels32 ();

						Debug.Log ("[DQC]:start push marker " + i+" #length is:"+colors.Length+" #width is:"+tex.width +" #height is:"+tex.height);

						unsafe{
							fixed(Color32* marker = &colors [0]) {
								pushMarker((char*)marker);
							}
						}
					}
					finishPushMarker();
				}

			}//end if w or h changed


			colors = webcamTexture.GetPixels32 ();

			Debug.Log ("[DQC]:start push image ");

			#if (USE_PREPROCESSED_IMAGE)

			unsafe{
			fixed(Color32* p = &colors [0]) {
				pushImageUnity ((char*)p,CallBackFunc2);
			}
			}
			#else
			unsafe{
				fixed(Color32* p = &colors [0]) {
					pushImage ((char*)p,CallBackFunc);
				}
			}
			#endif


			/*

			for (int i = 0; i < 5; i++) {
				Debug.Log (colors [i].r + " " + colors [i].g + " " + colors [i].b + " " + colors [i].a + "-----------------");
			}
			int sum = 0;
			for (int i = 0; i < colors.Length; i++)
				sum += colors [i].r+colors[i].g+colors[i].b+colors[i].a;

			bool equ;
			if (colors.Length == w * h)
				equ = true;
			else
				equ = false;
			Debug.Log ("!!!1!!!:" + sum + "   "+equ);

			unsafe{
				fixed(Color32* p = &colors [0]) {
					TestFuncP (w, h, (int*)p, CallBackFunc);
				}
			}

			//To see the order of the textures' pixels	
			int newlength=(int)(colors.Length);
			Color32[] newcolors=new Color32[newlength];
			for (int i=0;i<(int)(360);i++)
				newcolors[i]=colors[i];
			correctTex = new Texture2D (w,h);	
			correctTex.SetPixels32 (newcolors);
			correctTex.Apply ();
			GetComponent<Renderer> ().material.mainTexture = correctTex;
			*/
		}


		/*
			int w = webcamTexture.width;
			int h = webcamTexture.height;

			oldColors = webcamTexture.GetPixels32();
			Debug.Log ("[DQC]:color[]:" + oldColors.Length);

			bool newed = false;
			if (newColors == null || newColors.Length != oldColors.Length) {
				newColors = new Color32[oldColors.Length];
				correctTex = new Texture2D (h, w);	
				newed = true;
			}

		for (int i = 0; i < h; i++)
			for (int j = 0; j < w; j++) {
				int x =  i;
				int y = -j + w-1;
				newColors [y * h + x] = oldColors [i * w + j];
			}
			correctTex.SetPixels32 (newColors);
			correctTex.Apply ();

			if (newed) {
				GetComponent<Renderer> ().material.mainTexture = correctTex;		
			}

*/

	}
		

	static string PrintScreenCoord(Vector3 v)
	{
		const int f = 580;
		int screenx=(int)(v.x/v.z*f+lastw/2);
		int screeny=(int)(v.y/v.z*f+lasth/2);
		return "("+screenx+","+ screeny+") ";
	}

	static void PrintParamDebug(Result p)
	{
		unsafe{
		Debug.Log ("[DQC]:CallBackFunc.id:" + p.id + 
			"#\nPosition:\n" + p.Position[0]+" "+p.Position[1]+" "+p.Position[2]+
			"#\nRotation:\n"+ p.Rotation[0]+" "+ p.Rotation[1]+" "+p.Rotation[2]+" "+p.Rotation[3]+" "+p.Rotation[4]+" "+p.Rotation[5]+" "+ p.Rotation[6]+" "+p.Rotation[7]+" "+p.Rotation[8]+
			"#");

		string ts="[DQC]:cubeVertex:\n",ts2="";

		
		for (int t=0;t<8;t++)
		{
			Vector3 temp=new Vector3((float)p.cubeVertex[t*3+0],(float)p.cubeVertex[t*3+1],(float)p.cubeVertex[t*3+2]);
				ts+= temp.ToString();
		}
		Debug.Log(ts);

		//Debug.Log("[dqc]:quaternion:w:"+p.w+" x:"+p.x+" y:"+p.y+" z:"+p.z);
		}
	}


	[MonoPInvokeCallback(typeof(callback))]  
	static void CallBackFunc(IntPtr param,IntPtr num) { 
		unsafe{
			Result* rs=(Result*)param.ToPointer ();
			//var p = (Result)Marshal.PtrToStructure(param, typeof(Result));  

			#if (HIDE_WHEN_MISS)
			#if (USE_CUBE_VERTEX)
			for (int i=0;i<8;i++)
				vertexs_go[i].GetComponent<Renderer>().enabled=false;
			#endif
			#if (USE_CUBE_CENTER)
			for (int i=0;i<MAX_MARKER;i++)
				objects_go[i].GetComponent<Renderer>().enabled=false;
			#else
			for (int i=0;i<MAX_MARKER;i++)
				objects_go[i].SetActive(false);
			#endif
			#endif
		

			if (num==null || num.ToPointer()==null){
				Debug.Log("[dqc]:num==null!");
				return;
			}
			int n =  *((int*)num.ToPointer());
			Debug.Log("[dqc]num=="+n);
			if (n>MAX_MARKER)
			{
				Debug.Log("[dqc]:num too large! limit is "+MAX_MARKER+" markers");
				n=MAX_MARKER;
			}



			for (int ii=0;ii<n;ii++)
			{
				Debug.Log("[dqc]【Cube No. "+(ii+1)+"】:");

				Result p = (*rs++);
				PrintParamDebug(p);
				int id=p.id;

				////////set the object visible
				#if (USE_CUBE_VERTEX)
				for (int i=0;i<8;i++)
				vertexs_go[i].GetComponent<Renderer>().enabled=true;
				#endif
				#if (USE_CUBE_CENTER)
				objects_go[id].GetComponent<Renderer>().enabled=true;
				#else
					objects_go[id].SetActive(true);
				#endif
				//////////

				///////set the vertexs
				Vector3[] vertexs=new Vector3[8];
				if (markerFirstShow[id])
				{
					for (int t=0;t<8;t++)
						cubeVertexs[id,t]=new Vector3((float)p.cubeVertex[t*3+0],(float)p.cubeVertex[t*3+1],(float)p.cubeVertex[t*3+2]);
				}
				for (int t=0;t<8;t++)
					vertexs[t]=cubeVertexs[id,t];
				/////////////

					
				////!!! vt is P. mr is R. Xc=R*Xw+(-R*P)

				////////////////start caculating R///////////////
				Vector3 offset=new Vector3 (p.Position [0], p.Position [1], p.Position [2]);
				Matrix4x4 mr=new Matrix4x4();

				for (int i = 0; i < 3; i++)
					for (int j = 0; j < 3; j++) {
						mr [i, j] = p.Rotation [i * 3 + j];
					}

				Matrix4x4 objtranform = mr;
				offset = mr.MultiplyVector(offset);
				objtranform.m01 = -objtranform.m01;
				objtranform.m10 = -objtranform.m10;
				objtranform.m12 = -objtranform.m12;
				objtranform.m21 = -objtranform.m21;
				objtranform.m33 = 1;
				objtranform.m03 = -offset[0];
				objtranform.m13 = offset[1];
				objtranform.m23 = -offset[2];


				//right-hand coordinate to left-hand coordinate
				/*
				for (int i=0;i<4;i++)
					for (int j=0;j<4;j++)
						mwcl[i,j]=mwcr[i,j];

				mwcl.m01=-mwcl.m01;
				mwcl.m10=-mwcl.m10;
				mwcl.m12=-mwcl.m12;
				mwcl.m21=-mwcl.m21;
				mwcl.m13=-mwcl.m13;
*/
				/////////////////


				/////////////set the vertex position////////////////
				string ts,ts2;
				//right hand:
				ts="[dqc]right-hand vertexs after:\n";
				ts2="[dqc]right-hand screen coords after:\n";
				for (int i=0;i<8;i++)
				{
					vertexs[i]=objtranform.MultiplyPoint(vertexs[i]);
					//vertexs[i].y=-vertexs[i].y;
					#if (USE_CUBE_VERTEX)
					vertexs_go[i].transform.position = vertexs[i];	
					#endif
					ts2+=PrintScreenCoord(vertexs[i]);//screen coordinate after
					ts+= vertexs[i].ToString();
				}
				Debug.Log(ts);
				Debug.Log(ts2);
				///////////////////////////////////////



				/////////////////////////////// start caculating gameobjects' positions,etc /////////////////////////
				/// 
				#if (USE_CUBE_CENTER)
				GameObject tgo=objects_go[id];
				#else
				GameObject tgo=objects_go[id];
				#endif

				/////start caculating position////
				#if (USE_CUBE_CENTER) 
					//(if the pivot is the center of the object)
					Vector3 tv=new Vector3(0,0,0);
					for (int i=0;i<8;i++)
						tv+=vertexs[i];
					tv/=8.0f;

					Vector3 tv2=new Vector3(0,0,0);
					for (int i=0;i<4;i++)
						tv2+=vertexs[i];
					tv2/=4.0f;

					tgo.transform.position= (tv+tv2)/2.0f;
				#else 
					//(if the pivot is the bottom plane center of the object)
				/*
					Vector3 tv2=new Vector3(0,0,0);
					for (int i=0;i<4;i++)
						tv2+=vertexs[i];
					tgo.transform.position= tv2;
				*/

					Vector3 tv=new Vector3(0,0,0);
					for (int i=0;i<8;i++)
						tv+=vertexs[i];
					tv/=8.0f;

					Vector3 tv2=new Vector3(0,0,0);
					for (int i=0;i<4;i++)
						tv2+=vertexs[i];
					tv2/=4.0f;

					tgo.transform.position= (tv+tv2)/2.0f;
				#endif
				/////end caculating position////

				/////start caculating Euler Angles - Method 1
				/*
					double yaw =  Mathf.Atan2(mr.m01,mr.m00);//Ny/Nx
					double pitch =  Mathf.Atan2(mr.m02 ,(mr.m00* Mathf.Cos((float)yaw)+mr.m01 *Mathf.Sin((float)yaw)));//Nz/(Nx+Ny)
					double roll =  Mathf.Atan2((mr.m20 * Mathf.Sin((float)yaw)-mr.m21 * Mathf.Cos((float)yaw)),(-1*mr.m10 * Mathf.Sin((float)yaw)+mr.m11* Mathf.Cos((float)yaw)));

					const double pi=3.141592653589793;
					tgo.transform.eulerAngles = new Vector3((float)(roll/pi*180.0),(float)(pitch/pi*180.0),(float)(yaw/pi*180.0));
					//#if (!USE_CUBE_CENTER)
					//Vector3 offset_euler = tgo.transform.eulerAngles+EULER_ANGLE_OFFSET;
					//offset_euler.Set(-offset_euler.x+180f,offset_euler.y,offset_euler.z);
					//tgo.transform.eulerAngles=offset_euler;
					//#endif
					Debug.Log("[dqc]:eulerAngels 1:"+tgo.transform.eulerAngles);
					//Debug.Log("[dqc]: "+mr[0,1]+" , "+mr.m01+" , "+mr.m10);

					//tgo.transform.rotation.Set(p.x,p.y,p.z,p.w);
				*/
					/////////Method 2
				/*
				double e_x =  Mathf.Atan2(mwcl.m21,Mathf.Sqrt(Mathf.Pow(mwcl.m20,2)+Mathf.Pow(mwcl.m22,2)));
				double e_y =  Mathf.Atan2(-mwcl.m20,mwcl.m22);
				double e_z =  Mathf.Atan2(-mwcl.m01,mwcl.m11);
					*/
				//mr= mr*ROTATION_OFFSET ;

				/*
					double e_x=Mathf.Atan2(mr.m12,mr.m22);
					double e_y=-Mathf.Atan2(-mr.m02,Mathf.Sqrt(Mathf.Pow(mr.m12,2)+Mathf.Pow(mr.m22,2)));
					double e_z=Mathf.Atan2(mr.m01,mr.m00);

					tgo.transform.eulerAngles = new Vector3((float)(e_x/pi*180.0),(float)(e_y/pi*180.0),(float)(e_z/pi*180.0));
					Debug.Log("[dqc]:eulerAngels 2:"+tgo.transform.eulerAngles);
				*/

				////////Method 3

				Vector4 vy = objtranform.GetColumn(1);
				Vector4 vz = objtranform.GetColumn(2);

				Quaternion newQ = Quaternion.LookRotation(new Vector3(vz.x,vz.y,vz.z),new Vector3(vy.x,vy.y,vy.z));

				tgo.transform.rotation = newQ;


				///////end caculating Euler Angles//////


				///////start caculating scale
				#if (USE_CUBE_CENTER)
					float dist= Mathf.Sqrt( Mathf.Pow(tv.x-tv2.x,2)+Mathf.Pow(tv.y-tv2.y,2)+Mathf.Pow(tv.z-tv2.z,2));
					//float dist= Mathf.Sqrt( Mathf.Pow(vertexs[0].x-vertexs[4].x,2)+Mathf.Pow(vertexs[0].y-vertexs[4].y,2)+Mathf.Pow(vertexs[0].z-vertexs[4].z,2));
					Debug.Log("[dqc]:dist:"+dist);
					tgo.transform.localScale=new Vector3(dist,dist,dist);
				#else
				if (markerFirstShow[id]){
					float dist= Mathf.Sqrt( Mathf.Pow(tv.x-tv2.x,2)+Mathf.Pow(tv.y-tv2.y,2)+Mathf.Pow(tv.z-tv2.z,2))/ SCALE_OFFSET[id] ;
					tgo.transform.localScale=new Vector3(dist,dist,dist);
					//float dist= Mathf.Sqrt( Mathf.Pow(vertexs[0].x-vertexs[4].x,2)+Mathf.Pow(vertexs[0].y-vertexs[4].y,2)+Mathf.Pow(vertexs[0].z-vertexs[4].z,2))/2.0f;
					//tgo.transform.localScale = tgo.transform.localScale * (SCALE_OFFSET[id] * dist);
					Debug.Log("[dqc]:localScale:"+tgo.transform.localScale);
				}
				#endif
				//////end caculating scale

				markerFirstShow[id]=false;

				////////////////////////////// end caculating gameobjects' positions,etc //////////////////////
					
			}//for id
		}//unsafe

	}  


	static Texture2D cb_image;

	//使用这个callback需要把Plane的X轴旋转180度
	[MonoPInvokeCallback(typeof(callback))]  
	static void CallBackFunc2(IntPtr param,IntPtr num) {  		
		//var p = (Result)Marshal.PtrToStructure(param, typeof(Result));  
		unsafe{
			Result* rs=(Result*)param.ToPointer ();
			var p = *rs;
			PrintParamDebug(p);

			Vector3[] vertexs=new Vector3[8];
			for (int t=0;t<8;t++)
				vertexs[t]=new Vector3((float)p.cubeVertex[t*3+0],(float)p.cubeVertex[t*3+1],(float)p.cubeVertex[t*3+2]);


			if (p.img==null)
			{
				Debug.Log("[dqc]not valid!");
				colors=webcamTexture.GetPixels32();
				cb_image.SetPixels32(colors);
				cb_image.Apply();
				return;
			}
				
			for (int i=0;i<lastw*lasth;i++)
			{
				Color32 temp=new Color32();
				/*
				temp.b=(byte)(*pi++);
				temp.g=(byte)(*pi++);
				temp.r=(byte)(*pi++);
				*/
				temp.b=p.img[i*3+0];
				temp.g=p.img[i*3+1];
				temp.r=p.img[i*3+2];
				temp.a=255;
				colors[i]=temp;
			}

			if (cb_image == null)
				Debug.Log("[dqc]:callbackfunc_image: cb_image is null");
			cb_image.SetPixels32(colors);
			cb_image.Apply();
		}
	}




	/*
	[DllImport ("__Internal")]  
	//[DllImport("testosx")]
	unsafe static extern void TestFuncP(int w,int h,int* pc,CallBack cb);  

	delegate void CallBack(IntPtr param);  

	[MonoPInvokeCallback(typeof(CallBack))]  
	static void CallBackFunc(IntPtr param) {  
		var p = (Parameter)Marshal.PtrToStructure(param, typeof(Parameter));  
		//Debug.Log("[DQC]:in CallBackFunc. a:" + p.a + " b:" + p.b);  
		Debug.Log("!!!2!!!:"+p.a+"  "+p.b);
	}  
	*/
}