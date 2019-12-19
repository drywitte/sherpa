using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.UI;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.Features2dModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.ImgprocModule;


[RequireComponent(typeof(ARRaycastManager))]
public class SceneController : MonoBehaviour
{
	private WebCamTexture _webcam;
	private Texture2D _cameraTexture;
	private bool running = false;
	public Image CameraImage;
	private SimpleBlobDetector blobber;
    public ARSessionOrigin origin;
    private ARRaycastManager rcm;
    public GameObject cube;
    List<Vector3> keyPix = new List<Vector3>();
    Mat matImg;
    Mat contourMat;
    Texture2D imgText;
    Scalar color = new Scalar(255, 255, 255, 255);
    UnityEngine.Color32[] rawImage;
	// private SimpleBlobDetector.Params Params;

	/// <summary>
	/// Invoked whenever there's a touch.
	/// </summary>
	public static event Action onTouch;
	public ARRaycastManager m_RaycastManager;
	static List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();


	//private void Awake()
	//{
	//    rcm = origin.GetComponent<ARRaycastManager>();
	//    Debug.Assert(rcm);
	//}

	// Start is called before the first frame update
	void Start()
    {
		onTouch += ReactToTouch;
        // Application.targetFrameRate = 15;
		_webcam = new WebCamTexture();
		_webcam.Play();
        _cameraTexture = new Texture2D(_webcam.width, _webcam.height);
        CameraImage.material.mainTexture = _cameraTexture;
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
		Debug.Assert(_webcam.isPlaying);
        //string paramsPath = Utils.getFilePath("blobparams.yml");
        //blobber.read(paramsPath);
        Debug.Assert(Camera.main);
        Debug.Assert(_cameraTexture);
        Debug.Assert(_webcam);



        Utils.setDebugMode(true);

    }

    // Update is called once per frame
    void FixedUpdate()
    {
		if (Input.touchCount > 0)
		{
			Touch touch = Input.GetTouch(0);

			if (touch.phase == TouchPhase.Began)
			{
				if (m_RaycastManager.Raycast(touch.position, s_Hits, TrackableType.PlaneWithinPolygon))
				{
					Pose hitPose = s_Hits[0].pose;


					if (onTouch != null)
					{
						onTouch();
					}
				}
			}
		}

		if (running)
		{
			
			rawImage = _webcam.GetPixels32();
            matImg = new Mat(_webcam.height, _webcam.width, CvType.CV_8UC4);
            OpenCVForUnity.UnityUtils.Utils.webCamTextureToMat(_webcam, matImg);
            contourMat = getHolds(matImg);

            // create texture
            imgText = new Texture2D(contourMat.cols(), contourMat.rows());

			getKeyPoints(contourMat);
			//findColors(matImg);
			//_cameraTexture.SetPixels32(rawImage);


			//_cameraTexture.SetPixels32(contourText.GetPixels32());
			for (int i = 0; i < keyPix.Count; i++)
			{

				Imgproc.rectangle(matImg, new OpenCVForUnity.CoreModule.Rect((int)(keyPix[i][0] - keyPix[i][2] / 2), (int)(keyPix[i][1] - keyPix[i][2] / 2), (int)keyPix[i][2], (int)keyPix[i][2]), color);
				//quantize(keyPix[i], matImg);
				//_cameraTexture.SetPixels((int)keyPix[i][0] - (int)keyPix[i][2] / 2, (int)keyPix[i][1] - (int)keyPix[i][2] / 2, (int)keyPix[i][2], (int)keyPix[i][2], colors);
			}

			//update the camera's texture - for production
			Utils.matToTexture2D(matImg, imgText, true);
            // Debug.Log("Image width is " + imgText.width.ToString() + ". Image height is: " + imgText.height.ToString());
            // matImg.reshape(matImg.rows(), matImg.cols());
            // Imgproc.pyrUp(matImg, matImg);
            // Debug.Log("Mat img shape is " + matImg.size().ToString());

			// Update the camera's texture for testing purposes
			// Utils.matToTexture2D(contourMat, imgText, true);

			Texture oldTexture = CameraImage.material.mainTexture;
			Destroy(oldTexture);
			CameraImage.material.mainTexture = imgText;
			CameraImage.enabled = false;
            CameraImage.enabled = true;
            // TODO test if disposing these reduces lag?
            // matImg.Dispose();
            // contourMat.Dispose();

        }
	}



    // detect key points using blob detection and return a list of the key points in pixel form and their size
    void getKeyPoints(Mat matImg)
    {
        MatOfKeyPoint keypts = new MatOfKeyPoint();
        Mat hierarchy = new Mat();
        blobber.detect(matImg, keypts);
        // Debug.Log("key points are " + keypts.size());
        Mat imgWithKeyPts = new Mat();
        Features2d.drawKeypoints(matImg, keypts, imgWithKeyPts, new Scalar(255, 255, 255), 4);
        Texture2D matToText = new Texture2D(imgWithKeyPts.cols(), imgWithKeyPts.rows(), TextureFormat.RGBA32, false);
        KeyPoint[] keyPtArray = keypts.toArray();
        keyPix.Clear();
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
        Destroy (matToText);
    }


    // image manipulation to identify contours, convex hulls, and return a mask that we can then run getkeypoints on
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
        Mat mask = Mat.zeros(matImg.rows(), matImg.cols(), CvType.CV_8UC4);
		MatOfPoint e = new MatOfPoint();
		//for (int i = 0; i < hullInts.Count; i++)
		//{
		//   for (int j = 0; j < contours.Count; j++)    
		//    {
		//        //hullPts.Add(new MatOfPoint(contours[j][hullInts[i]]));
		//    }
		//}
		for (int i = 0; i < contours.Count; i++)
		{
			listPo.Clear();
			hullPts.Clear();
			for (int j = 0; j < hullInts[i].toList().Count; j++)
			{
				listPo.Add(contours[i].toList()[hullInts[i].toList()[j]]);
				
			}
			e.fromList(listPo);
			hullPts.Add(e);
			Imgproc.drawContours(mask, hullPts, 0, new Scalar(0, 255, 0), -4);
			e = new MatOfPoint();
		}

		// create mask of hulls
        // Debug.Log("Contour is " + contourMat.size().ToString());
        matImg.copyTo(contourMat, mask);
        Imgproc.pyrUp(mask, mask);
        // Debug.Log("Sized up mat img size is : " + matImg.size().ToString());
        
		Imgproc.cvtColor(contourMat, contourMat, Imgproc.COLOR_BGR2GRAY);
        // Debug.Log("Mask img size is : " + mask.size().ToString());
        
        // dispose
        hierarchy.Dispose();
        mask.Dispose();
        binary.Dispose();
        threshold.Dispose();
        edges.Dispose();
        e.Dispose();
        contours.Clear();


        return contourMat;
	}


    // quantizes colors
    void quantize(Vector3 roi, Mat matImg)
    {
        Mat kScores = new Mat();
        TermCriteria end = new TermCriteria();
        end.type = TermCriteria.COUNT;
        Mat submat = matImg.submat(new Range((int)roi[0], (int)roi[1]), new Range((int)(roi[0] + roi[2]), (int)(roi[1] + roi[2])));
        Core.kmeans(submat, 4, kScores, end , 4, 0);
        Debug.Log(kScores);
    }


    // finds list of colors in the image
    List<(double, double)> findColors(Mat matImg)
    {
        List<(double, double)> colors = new List<(double, double)>();
        if (keyPix.Count > 0)
        {
            // convert to hsv
            Mat hsvImg = new Mat();
            Imgproc.cvtColor(matImg, hsvImg, 53);

            // set color for each keypoint
            for (int i = 0; i < keyPix.Count; i++)
            {
                //colors.Add(binColor(new Vector2Int((int)keyPix[i][0]+ (int)keyPix[i][2]/2, (int)keyPix[i][1] + (int)keyPix[i][2]/2), new Vector2Int((int)keyPix[i][0] - (int)keyPix[i][2]/2, (int)keyPix[i][1] - (int)keyPix[i][2]/2), hsvImg));     
            }
        }

        return colors;
    }


    // bins the colors in the image
    List<(double, double)> binColor(Vector2Int topLeft, Vector2Int bottomRight, Mat hsvImg)
    {
        Debug.Log("bin color called");
        Mat mask = new Mat(_webcam.height, _webcam.width, CvType.CV_8UC4);
        mask.submat(new Range(topLeft[1], bottomRight[1]), new Range(topLeft[0], bottomRight[0]));
        MatOfInt hist = new MatOfInt();
        List <Mat> hsvList = new List<Mat>();
        hsvList.Add(hsvImg);
        int binLen = 4;
        int numBins = 256 / binLen;

        for (int i = 0; i < 3; i++)
        {
            Imgproc.calcHist(hsvList, new MatOfInt(i), mask, new MatOfInt(numBins), hist, new MatOfFloat(0, 256));
        }
        Core.MinMaxLocResult results = Core.minMaxLoc(hist);
        List<(double, double)> color = new List<(double, double)>();
        for (int i = 0; i < 3; i++) {
            color.Add((results.maxVal * binLen, results.maxVal));
        }

        return color;
    }


    // used to start/stop the app on button click
    public void onBlob()
    {
		//running = !running;
		//Debug.Log("running is " + running);
		//_cameraTexture = new Texture2D(_webcam.width, _webcam.height);
		//CameraImage.material.mainTexture = _cameraTexture;
		//CameraImage.sprite = null;
    }

	void ReactToTouch()
    {
		running = !running;
	}
}
