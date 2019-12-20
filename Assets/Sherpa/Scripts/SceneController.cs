using System;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.Features2dModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.ImgprocModule;
using System.Collections.Generic;



/// <summary>
/// This component tests getting the latest camera image
/// and converting it to RGBA format. If successful,
/// it displays the image on the screen as a RawImage
/// and also displays information about the image.
///
/// This is useful for computer vision applications where
/// you need to access the raw pixels from camera image
/// on the CPU.
///
/// This is different from the ARCameraBackground component, which
/// efficiently displays the camera image on the screen. If you
/// just want to blit the camera texture to the screen, use
/// the ARCameraBackground, or use Graphics.Blit to create
/// a GPU-friendly RenderTexture.
///
/// In this example, we get the camera image data on the CPU,
/// convert it to an RGBA format, then display it on the screen
/// as a RawImage texture to demonstrate it is working.
/// This is done as an example; do not use this technique simply
/// to render the camera image on screen.
/// </summary>
public class SceneController: MonoBehaviour
{

	public GameObject cube;
	public GameObject textDistancePrefab;
	public GameObject visualizer;
	public Camera cam;
	public ARSessionOrigin origin;
	public Button nextActionButton;

	/// <summary>
	/// Get or set the <c>ARCameraManager</c>.
	/// </summary>
	public ARCameraManager cameraManager
	{
		get { return m_CameraManager; }
		set { m_CameraManager = value; }
	}

	/// <summary>
	/// The UI RawImage used to display the image on screen.
	/// </summary>
	public RawImage rawImage
	{
		get { return m_RawImage; }
		set { m_RawImage = value; }
	}

	//public RawImage colorImage;


	private SimpleBlobDetector blobber;
	List<Vector3> keyPix = new List<Vector3>();
	Mat matImg;
	Mat contourMat;
	Texture2D imgText;
	Scalar color = new Scalar(255, 255, 255, 255);
	private ARRaycastManager rayCastManager;
	static List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();
	static List<ARRaycastHit> s_Hits2 = new List<ARRaycastHit>();
	Ray ray;

	private GameObject gamePiece;

	private LineRenderer lineRenderer;
	private GameObject instantiatedVisualizer;

	private List<GameObject> cubes = new List<GameObject>();
    
	int imageHeight;
	int imageWidth;
	Color[] pixels;
	Color32[] pixels_32;
	private List<Color32>colors = new List<Color32>();
	private List<Mat>submats = new List<Mat>();
	List<XRCameraConfiguration> configurations;
	double scaleFactor;
	int downsample = 1;

    private bool enableKeypointDetection;
	private bool enableGlobalCubePlacement;
	private bool enableLineDrawing;
	private bool enableColorSelection;

	private int selectedColorSide = 50;
	Text actionText;

	private List<Vector3> selectedCubePositions = new List<Vector3>();

	private Texture2D m_rotated;

	private List<Vector3> cubeDestinations = new List<Vector3>();
	private List<Vector3> cubePositionsUp = new List<Vector3>();
	

	[SerializeField]
	[Tooltip("The ARCameraManager which will produce frame events.")]
	ARCameraManager m_CameraManager;


	[SerializeField]
	RawImage m_RawImage;

	[SerializeField]
	Text m_ImageInfo;

	/// <summary>
	/// The UI Text used to display information about the image on screen.
	/// </summary>
	public Text imageInfo
	{
		get { return m_ImageInfo; }
		set { m_ImageInfo = value; }
	}

	void OnEnable()
	{
		if (m_CameraManager != null)
		{
			m_CameraManager.frameReceived += OnCameraFrameReceived;
		}
	}

	void OnDisable()
	{
		if (m_CameraManager != null)
		{
			m_CameraManager.frameReceived -= OnCameraFrameReceived;
		}
	}

void Start()
    {
		m_RawImage.enabled = false;

		actionText = nextActionButton.GetComponentInChildren<Text>();
		m_ImageInfo.gameObject.SetActive(false);
		enableKeypointDetection = false;
	    enableGlobalCubePlacement = false;
	    enableLineDrawing = false;
		enableColorSelection = false;
		nextActionButton.onClick.AddListener(onNextActionPress);

		Debug.Log("Pixel height is " + cam.scaledPixelHeight);
		Debug.Log("Pixel width is " + cam.scaledPixelWidth);
		rayCastManager = origin.GetComponent<ARRaycastManager>();
        Debug.Log(rayCastManager);
		instantiatedVisualizer = Instantiate(visualizer, new Vector3(0, 0, 0), new Quaternion(0, 0, 0, 0));
		lineRenderer = instantiatedVisualizer.GetComponent<LineRenderer>();
		lineRenderer.positionCount = 0;
		// Application.targetFrameRate = 15;
		Params blobParams = new Params();
        blobParams.set_minThreshold(0);
        blobParams.set_maxThreshold(255);
        blobParams.set_filterByArea(true);
        blobParams.set_minArea(25);
        blobParams.set_filterByCircularity(false);
        blobParams.set_minCircularity(.1f);
        blobParams.set_filterByConvexity(false);
        blobParams.set_minConvexity(.1f);
        blobParams.set_filterByInertia(true);
        blobParams.set_minInertiaRatio(.05f);

		blobber = SimpleBlobDetector.create();

		//blobber = SimpleBlobDetector.create(blobParams);
		//blobber = SimpleBlobDetector.create(blobParams);
		Debug.Log((Screen.width/2).ToString());
        // string paramsPath = Utils.getFilePath("blobparams.yml");
        //blobber.read(paramsPath);
		
		// downsize screen resolution
		Screen.SetResolution((int) Screen.width/2, (int) Screen.height/2, true, 30);
        Utils.setDebugMode(true);
		Application.targetFrameRate = 30;
    }

    void onNextActionPress()
	{
        // Starting the calibration 
		if (actionText.text == "Calibrate")
		{
			enableKeypointDetection = true;
			enableGlobalCubePlacement = true;
			actionText.text = "Calibration Done";
		
		} else if(actionText.text == "Calibration Done")
		{
			enableKeypointDetection = false;
			enableGlobalCubePlacement = false;
			actionText.text = "Reset";
			//nextActionButton.gameObject.SetActive(false);
			m_ImageInfo.gameObject.SetActive(true);
			enableColorSelection = true;
			Debug.Log("Colors list: " + colors.ToString());
			for (int i = 0; i < cubes.Count; i++)
			{
				Vector3 cubeTransform = cubes[i].transform.position;
				cubeDestinations.Add(cubeTransform);
				cubes[i].transform.position = new Vector3(cubeTransform.x, 0, cubeTransform.z);
				colors.Add(kmeansColor(submats[i]));
				Debug.Log("Length of colors is: " + colors.Count.ToString());
				Debug.Log("color is " + colors[i].ToString());
				cubes[i].GetComponent<Renderer>().material.color = Color.black;
				cubes[i].GetComponent<Renderer>().material.color = colors[i];
				Debug.Log("Actual cube color is " + cubes[i].GetComponent<Renderer>().material.color.ToString());
			}
		}
		else if(actionText.text == "Reset")
		{
			selectedCubePositions.Clear();
			lineRenderer.positionCount = 0;
			//setupRawImage(new Color(255f,255f,255f));

            enableGlobalCubePlacement = true;
			//nextActionButton.gameObject.SetActive(false);
			//m_ImageInfo.gameObject.SetActive(true);
			for (int i = 0; i < cubes.Count; i++)
			{
				Destroy(cubes[i]);
			}
			cubes.Clear();
			// colors.Clear();
			addKeyPixCubes();
			//lineRenderer.positionCount = 0;
			enableColorSelection = true;
		}
	}

    private void addKeyPixCubes()
	{
		// keyPix.Sort(SortByX);
		for (int i = 0; i < keyPix.Count; i++)
		{
			// convert to viewpoint
			Vector3 default_coords = new Vector3(2 * keyPix[i][1] * downsample, Screen.height - 2 * (keyPix[i][0]) * downsample, 0);
			if (enableGlobalCubePlacement)
			{
				if (rayCastManager.Raycast(default_coords, s_Hits, TrackableType.PlaneWithinPolygon))
				{
					Pose hitPose = s_Hits[0].pose;
					gamePiece = Instantiate(cube, hitPose.position, origin.transform.rotation);
					// gamePiece.GetComponent<Renderer>().material.color = colors[i];
					// gamePiece.transform.localScale = new Vector3(gamePiece.transform.localScale.x + keyPix[i][2], gamePiece.transform.localScale.y + keyPix[i][2], gamePiece.transform.localScale.z + keyPix[i][2]);
					cubes.Add(gamePiece);
					Debug.Log("Target position " + hitPose.position + " & Target rotation " + hitPose.rotation);
				}
			}

		}
		for (int j = 0; j < colors.Count; j++) {
				cubes[j].GetComponent<Renderer>().material.color = Color.black;
				cubes[j].GetComponent<Renderer>().material.color = colors[j];
				Debug.Log("kmeans colors applied");
		}	
	}

	void Update() {

		if (enableColorSelection)
		{
            // Go through every cube
            // Lerp it towards the final desitnation

            for(int i = 0; i < cubes.Count; i++)
			{
				cubes[i].transform.position = Vector3.Lerp(cubes[i].transform.position, cubeDestinations[i], Time.deltaTime);
			}
		}


		if (enableColorSelection && Input.touchCount > 0)
		{

			Touch touch = Input.GetTouch(0);

			if (touch.phase == TouchPhase.Began)
			{
				Debug.Log("Update Log: Inisde second if");

				Ray ray = Camera.main.ScreenPointToRay(touch.position);
				RaycastHit raycastHit;
				if(Physics.Raycast(ray, out raycastHit))
				{
					Debug.Log("Here is the raycastHit");
					Debug.Log(raycastHit);
					//RaycastHit2D hit = Physics2D.Raycast(touch.position, Vector2.zero);
					Debug.Log("Update Log: After hit");
					Debug.Log("Collider name is " + raycastHit.collider.name);
					if (raycastHit.collider.name == "Cube(Clone)")
					{
						Debug.Log("Update Log: Inisde third if");
						selectedCubePositions.Add(raycastHit.transform.position);
						//selectedCubePositions.Add(raycastHit.point);
						lineRenderer.positionCount = selectedCubePositions.Count;
						Debug.Log("Update Log: About to go into for");

						for (int j = 0; j < selectedCubePositions.Count; j++)
						{
							lineRenderer.SetPosition(j, selectedCubePositions[j]);
						}
						Debug.Log("Update Log: Done with for");

					}
				}
			}
			//nextActionButton.gameObject.SetActive(true);
		}
	}


    // List of keypoints is global - keyPix
    private List<Vector3> getEnabledKeypoints(Color color)
	{
		// keyPix.Sort(SortByX);
		// Dummy code 
		List<Vector3> enabledKeyPix = new List<Vector3>();
        if(keyPix.Count >= 2)
		{
			enabledKeyPix.Add(keyPix[0]);
			enabledKeyPix.Add(keyPix[1]);
		} else if(keyPix.Count == 1)
		{
			enabledKeyPix.Add(keyPix[0]);
		}
		return enabledKeyPix;
	}

 //   private void setupRawImage(Color color)
	//{
 //       if(colorImage.texture != null)
	//	{
	//		Texture2D oldTexture = (Texture2D) colorImage.texture;
	//		Destroy(oldTexture);
	//	}
	//	Debug.Log("SELECTED COLOR WAS " + color);

	//	var format = TextureFormat.RGBA32;
	//	Texture2D colorTexture = new Texture2D(selectedColorSide, selectedColorSide, format, false);
	//	var fillColorArray = colorTexture.GetPixels();
	//	for (var i = 0; i < fillColorArray.Length; ++i)
	//	{
	//		fillColorArray[i] = color;
	//	}

	//	colorTexture.SetPixels(fillColorArray);
	//	colorTexture.Apply();
	//	colorImage.texture = colorTexture;	
	//}



	unsafe void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
	{
		if (!enableKeypointDetection)
		{
			return;
		}
		// Attempt to get the latest camera image. If this method succeeds,
		// it acquires a native resource that must be disposed (see below).
		XRCameraImage image;
		if (!cameraManager.TryGetLatestImage(out image))
		{
			return;
		}

	
		// Once we have a valid XRCameraImage, we can access the individual image "planes"
		// (the separate channels in the image). XRCameraImage.GetPlane provides
		// low-overhead access to this data. This could then be passed to a
		// computer vision algorithm. Here, we will convert the camera image
		// to an RGBA texture and draw it on the screen.

		// Choose an RGBA format.
		// See XRCameraImage.FormatSupported for a complete list of supported formats.
		var format = TextureFormat.RGBA32;

		if (m_Texture == null || m_Texture.width != image.width || m_Texture.height != image.height)
		{
			m_Texture = new Texture2D(image.width, image.height, format, false);
			imageHeight = image.height;
			imageWidth = image.width;
		}

		// Convert the image to format, flipping the image across the Y axis.
		// We can also get a sub rectangle, but we'll get the full image here.
		var conversionParams = new XRCameraImageConversionParams(image, format,
            CameraImageTransformation.None);

		// Texture2D allows us write directly to the raw texture data
		// This allows us to do the conversion in-place without making any copies.
		var rawTextureData = m_Texture.GetRawTextureData<byte>();
		try
		{
			image.Convert(conversionParams, new IntPtr(rawTextureData.GetUnsafePtr()), rawTextureData.Length);
		}
		finally
		{
			// We must dispose of the XRCameraImage after we're finished
			// with it to avoid leaking native resources.
			image.Dispose();
		}


		// Apply the updated texture data to our texture
		m_Texture.Apply();
		m_rotated = m_Texture;
		
		// set scaling for camera width to screen height
		scaleFactor = (double) Screen.height/image.width;
		Debug.Log("Scale factor is " + scaleFactor.ToString() +  " Resized height should be " + (scaleFactor * image.height).ToString());
		// resize texture
		TextureScale.Point(m_rotated, (int) (m_rotated.width * scaleFactor)/downsample, (int) (m_rotated.height*scaleFactor)/downsample);
		m_rotated.Apply();
		Debug.Log("actual resized height is " + m_rotated.height.ToString());
		
		// get cropped from resized texture & crop
		pixels = m_rotated.GetPixels(0, (int) (m_rotated.height - Screen.width/downsample)/2, (int) Screen.height/downsample, (int) Screen.width/downsample, 0);
		Destroy(m_rotated);

		// load cropped pixels into new texture
		Texture2D cropped_tex = new Texture2D((int) Screen.height/downsample, (int) Screen.width/downsample, TextureFormat.RGBA32, false);
		cropped_tex.SetPixels(pixels, 0);
		cropped_tex.Apply();
	
		for (int i = 0; i < cubes.Count; i++) {
			Destroy(cubes[i]);
		}
		cubes.Clear();

		// create mat of contours using scaled down cropped image
		matImg = new Mat(cropped_tex.height, cropped_tex.width, CvType.CV_8UC4);
		OpenCVForUnity.UnityUtils.Utils.texture2DToMat(cropped_tex, matImg);
		Destroy(cropped_tex);
		contourMat = getHolds(matImg);

		// create texture
		imgText = new Texture2D(contourMat.cols(), contourMat.rows(), TextureFormat.RGBA32, false);

		getKeyPoints(contourMat);
		Debug.Log("Length of keypoints is " + keyPix.Count.ToString());
		for (int i = 0; i < keyPix.Count; i++)
		{
			Debug.Log("Mat img is " + matImg);
			submats.Add(getSubmat(keyPix[i], matImg));
			// Color col = quantize(keyPix[i], matImg);
			// Imgproc.rectangle(matImg, new OpenCVForUnity.CoreModule.Rect((int)(keyPix[i][0] - keyPix[i][2] / 2), (int)(keyPix[i][1] - keyPix[i][2] / 2), (int)keyPix[i][2], (int)keyPix[i][2]), new Scalar(col.r, col.g, col.b, col.a), -4, 8);
			// colors.Add(col);
		}
		Debug.Log("Length of submats is " + submats.Count.ToString());
		addKeyPixCubes();

		Utils.matToTexture2D(matImg, imgText, true);

        //disposal
		if(m_RawImage.texture != null)
		{
			Destroy(m_RawImage.texture);
			// Destroy(cropped_tex);
			// Destroy(m_rotated);
		}
		
		// Set the RawImage's texture so we can visualize it.
		m_RawImage.texture = imgText;
		rawImage.SetNativeSize();
	}


    void getKeyPoints(Mat matImg)
    {	
		keyPix.Clear();
		submats.Clear();
        MatOfKeyPoint keypts = new MatOfKeyPoint();
        Mat hierarchy = new Mat();
        blobber.detect(matImg, keypts);
        // Debug.Log("key points are " + keypts.size());
        Mat imgWithKeyPts = new Mat();
        Features2d.drawKeypoints(matImg, keypts, imgWithKeyPts, new Scalar(255, 255, 255), 4);
        Texture2D matToText = new Texture2D(imgWithKeyPts.cols(), imgWithKeyPts.rows(), TextureFormat.RGBA32, false);
        KeyPoint[] keyPtArray = keypts.toArray();
        for (int i = 0; i < keyPtArray.Length; i++)
        {
            //keyPix.Add(Camera.main.ScreenToWorldPoint(new Vector3((float)keyPtArray[i].pt.x, (float)keyPtArray[i].pt.y), Camera.MonoOrStereoscopicEye.Mono));
            keyPix.Add(new Vector3((float)keyPtArray[i].pt.x, (float)keyPtArray[i].pt.y, keyPtArray[i].size));
            //keyPix3.Add(Camera.main.ScreenToViewportPoint(new Vector3((int)keyPtArray[i].pt.x, (int)keyPtArray[i].pt.y, 0)));
        }

		// disposal
		Destroy(matToText);
        imgWithKeyPts.Dispose();
        keypts.Dispose();
        hierarchy.Dispose();
    }


	Mat getHolds(Mat matImg)
    {
        // Debug.Log("Mat img size was: " + matImg.size().ToString());
        Imgproc.pyrDown(matImg, matImg);
        // Debug.Log("Mat img size is now: " + matImg.size().ToString());
        Mat binary = new Mat(matImg.rows(), matImg.cols(), CvType.CV_8UC4);
        Imgproc.GaussianBlur(matImg, binary, new Size(5, 5), (double)0.0);

        // create grayscale mat
        Imgproc.cvtColor(binary, binary, Imgproc.COLOR_BGR2GRAY);
        List<MatOfPoint> contours = new List<MatOfPoint>();

        // find threshold for edges
        Mat threshold = new Mat();
        Imgproc.threshold(binary, threshold, 0, 255, Imgproc.THRESH_BINARY + Imgproc.THRESH_OTSU);

        // find edges
        Mat edges = new Mat();
        // not sure abou this
        Imgproc.Canny(threshold, edges, 50, 50, 3);

        // find contours
        Mat hierarchy = new Mat();
        OpenCVForUnity.ImgprocModule.Imgproc.findContours(edges, contours, hierarchy, Imgproc.RETR_LIST, Imgproc.CHAIN_APPROX_SIMPLE);

        // find hulls
        List<MatOfInt> hullInts = new List<MatOfInt>();
        for (int i = 0; i < contours.Count; i++)
        {
            MatOfInt hull = new MatOfInt();
            Imgproc.convexHull(new MatOfPoint(contours[i].toArray()), hull);
            hullInts.Add(hull);
        }
		// Debug.Log("Count of hulls is " + hullInts.Count.ToString());

        // add hulls to a list
        List<MatOfPoint> hullPts = new List<MatOfPoint>();
		List<Point> listPo = new List<Point>();
		Mat contourMat = Mat.zeros(matImg.rows(), matImg.cols(), CvType.CV_8UC4); //new Mat(_webcam.height, _webcam.width, CvType.CV_8UC4);
        // Mat mask = Mat.zeros(matImg.rows(), matImg.cols(), CvType.CV_8UC4);
		// MatOfPoint e = new MatOfPoint();

		// for (int i = 0; i < contours.Count; i++)
		// {
		// 	listPo.Clear();
		// 	hullPts.Clear();
		// 	for (int j = 0; j < hullInts[i].toList().Count; j++)
		// 	{
		// 		listPo.Add(contours[i].toList()[hullInts[i].toList()[j]]);
				
		// 	}
		// 	e.fromList(listPo);
		// 	hullPts.Add(e);
		// 	Imgproc.drawContours(mask, hullPts, 0, new Scalar(0, 255, 0), -4);
		// 	e = new MatOfPoint();
		// }

		// create mask of hulls
        matImg.copyTo(contourMat);  //mask);
        // Imgproc.pyrUp(mask, mask);
        
		Imgproc.cvtColor(contourMat, contourMat, Imgproc.COLOR_BGR2RGBA);
        
        // dispose
        hierarchy.Dispose();
        // mask.Dispose();
        binary.Dispose();
        threshold.Dispose();
        edges.Dispose();
        // e.Dispose();
        contours.Clear();

        return contourMat;
	}


    // Really Y because of rotation
	static int SortByX(Vector3 p1, Vector3 p2)
	{
		return p1.x.CompareTo(p2.x);
	}

	// https://forum.unity.com/threads/how-to-pick-color-in-the-screen-point-x-y.133068/
	public Color getColorAtPress(int x, int y)//:Texture2D
	{
		Texture2D tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
		tex.ReadPixels(new UnityEngine.Rect(0, 0, Screen.width, Screen.height), 0, 0);
		tex.Apply();
		Debug.Log("Position of the touch is x: " + x + " and y is " + y);
		Color selectedColor = tex.GetPixel(x, y);
		Debug.Log("Pixel at touch is R: " + selectedColor.r + "G " + selectedColor.g + "B: " + selectedColor.b);
		return selectedColor;
	}

	Mat getSubmat(Vector3 roi, Mat matImg)  {
        Debug.Log("The matrix height: " + matImg.rows() + " and width is: "+ matImg.cols());
        Debug.Log("key point center is " + roi[0] + ", " + roi[1] + " and size is " + roi[2]);
        int boundX;
        int boundY;

        // ensure we don't go over bounds of image
        if (roi[0] + roi[2] > matImg.cols()) {
            boundX = matImg.cols();
        }
        else {
            boundX = (int)(roi[0] + roi[2]);
        }
        if (roi[1] + roi[2] > matImg.rows()) {
            boundY = matImg.rows();
        }
        else {
            boundY = (int)(roi[1] + roi[2]);
        }
        Debug.Log("Bound x is :" + boundX + " and bound y is: " + boundY);
        Mat submat = matImg.submat(new Range((int)roi[1], boundY), new Range((int)roi[0], boundX));
        // Imgproc.cvtColor(submat, submat, Imgproc.COLOR_BGR2HSV);
		Debug.Log("submat is " + submat);
        submat.convertTo(submat, CvType.CV_32F);
		return submat;
	}

	 // quantizes colors
    Color kmeansColor(Mat submat)
    {
        Mat kScores = new Mat();
        Mat centers = new Mat();
        TermCriteria end = new TermCriteria();
        end.type = TermCriteria.COUNT;
		end.maxCount = 2;
        Core.kmeans(submat, 4, kScores, end , 2, 0, centers);
        Color col = new Color32((byte)(255 * centers.get(0, 0)[0]), (byte)(255 * centers.get(0, 1)[0]), (byte)(255 * centers.get(0, 2)[0]), (byte)(255 * centers.get(0, 3)[0]));
		Debug.Log("Center has # cols: " + centers.cols().ToString());
        Debug.Log("color is " + centers.get(0, 1)[0].ToString() + " " + centers.get(0, 1)[0].ToString() + " " + centers.get(0, 2)[0].ToString() + " ");
        Debug.Log("Centers height: " + centers.rows() + " and width: " + centers.cols());
		submat.Dispose();
		kScores.Dispose();
		centers.Dispose();
		return col;
    }


	Texture2D m_Texture;
}
