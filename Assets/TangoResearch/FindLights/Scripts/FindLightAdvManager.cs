﻿using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using Tango;
using System.Collections.Generic;
using System.Threading;
using System;

public class FindLightAdvManager : MonoBehaviour, ITangoVideoOverlay, ITangoLifecycle
{

    public enum SuperpixelMethod { SLIC, CWATERSHED, NONE };
    public enum ScreenMode { MESH3D, ALBEDO, ERROR, RESULT };
    public SuperpixelMethod _CurSuperpixelMethod = SuperpixelMethod.SLIC;
    public ScreenMode _CurScreenMode = ScreenMode.RESULT;

    public Transform _PanelOptionsSLIC;
    public Transform _PanelOptionsWatershed;

    public Toggle _ToggleRealtime;
    public Toggle _ToggleSLIC;
    public Toggle _ToggleCWatershed;
    public Toggle _ToggleDebugLights;

    public Toggle _ToggleScreenMesh3D;
    public Toggle _ToggleScreenAlbedo;
    public Toggle _ToggleScreenError;
    public Toggle _ToggleScreenResult;

    public Slider _SliderClusterCount;
    public Slider _SliderResDiv;
    public Slider _SliderMaxIterations;
    public Slider _SliderBorderThreshold;
    public Slider _SliderCompactness;
    public Slider _SliderDebugLightX;
    public Slider _SliderDebugLightY;
    public Slider _SliderDebugLightZ;
    public Slider _SliderDebugLightXo;
    public Slider _SliderDebugLightYo;
    public Slider _SliderDebugLightZo;

    private TangoApplication _tangoApplication;

    public RenderTexture _InTexture;
    public RenderTexture _Mesh3DTexture;
    public Texture2D _AlbedoTexture;
    public Texture2D _ErrorTexture;
    

    private TangoUnityImageData _lastImageBuffer = null;

    public WatershedSegmentation _Watershed;
    public SLICSegmentation _SLIC;
    private FindLightAdvanced _FLA;

    public int _ClusterCount = 32;
    private int _maxIterations = 1;
    public int _ResDiv = 16;
    public float _ErrorThreshold = 0.001f;
    public int _BorderThreshold = 6;
    public int _Compactness = 10;

    private static int _rSliderClusterCountMax = 128;
    private static int _sliderClusterCountMax = 400;
    private static int _rSliderResLevelMax = 9;
    private static int _sliderResLevelMax = 11;
    private VectorInt3 _debugLightPos = new VectorInt3(0, 0, 0);

    private bool _debuggingLightPos = false;
    private bool _realtime = false;

    public Camera _Cam3DMesh;
    //public Material _ResultMat;
    //public Material _IoMat;
    public Transform _DebugLightOriginal;
    public Transform _DebugPointLight;
    public RawImage _RawImageScreen;

    public void Start()
    {
        _ToggleRealtime.onValueChanged.AddListener(value => onValueChangedRealtime(value));
        _ToggleSLIC.onValueChanged.AddListener(value => onValueChangedSuperpixelMethod(value, SuperpixelMethod.SLIC));
        _ToggleCWatershed.onValueChanged.AddListener(value => onValueChangedSuperpixelMethod(value, SuperpixelMethod.CWATERSHED));
        _ToggleDebugLights.onValueChanged.AddListener(value => onValueChangedDebugLights(value));

        _ToggleScreenAlbedo.onValueChanged.AddListener(value => onValueChangedScreenMode(ScreenMode.ALBEDO));
        _ToggleScreenError.onValueChanged.AddListener(value => onValueChangedScreenMode(ScreenMode.ERROR));
        _ToggleScreenMesh3D.onValueChanged.AddListener(value => onValueChangedScreenMode(ScreenMode.MESH3D));
        _ToggleScreenResult.onValueChanged.AddListener(value => onValueChangedScreenMode(ScreenMode.RESULT));

        _SliderResDiv.onValueChanged.AddListener(onValueChangedResDiv);
        _SliderClusterCount.onValueChanged.AddListener(onValueChangedClusterCount);
        _SliderMaxIterations.onValueChanged.AddListener(onValueChangedMaxIterations);
        _SliderBorderThreshold.onValueChanged.AddListener(onValueChangedBorderThreshold);
        _SliderCompactness.onValueChanged.AddListener(onValueChangedCompactness);
        _SliderDebugLightX.onValueChanged.AddListener(onValueChangedDebugLightX);
        _SliderDebugLightY.onValueChanged.AddListener(onValueChangedDebugLightY);
        _SliderDebugLightZ.onValueChanged.AddListener(onValueChangedDebugLightZ);
        _SliderDebugLightXo.onValueChanged.AddListener(onValueChangedDebugLightXo);
        _SliderDebugLightYo.onValueChanged.AddListener(onValueChangedDebugLightYo);
        _SliderDebugLightZo.onValueChanged.AddListener(onValueChangedDebugLightZo);

        _tangoApplication = FindObjectOfType<TangoApplication>();
        if (_tangoApplication != null)
        {
            _tangoApplication.Register(this);
        }

        _AlbedoTexture = new Texture2D(1280 / _ResDiv, 720 / _ResDiv);
        _AlbedoTexture.filterMode = FilterMode.Point;
        _AlbedoTexture.mipMapBias = 0;

        _Cam3DMesh.targetTexture = new RenderTexture((int)(1280 / (float)_ResDiv), (int)(720 / (float)_ResDiv), 16, RenderTextureFormat.ARGB32);
        _Cam3DMesh.targetTexture.filterMode = FilterMode.Bilinear;
        _Cam3DMesh.targetTexture.mipMapBias = 0;

        _Watershed = new WatershedSegmentation();
        _Watershed._ClusterCount = _ClusterCount;
        _Watershed._BorderThreshold = _BorderThreshold;

        _SLIC = new SLICSegmentation();
        _SLIC.MaxIterations = _maxIterations;
        _SLIC.ResidualErrorThreshold = _ErrorThreshold;
        _SLIC.Compactness = _Compactness;

        _FLA = GetComponent<FindLightAdvanced>();
        _FLA.SetupLightErrorGrid();

        _SliderResDiv.maxValue = _sliderResLevelMax;
        _SliderClusterCount.maxValue = _sliderClusterCountMax;

    }

    private void onValueChangedDebugLightXo(float value)
    {
        Vector3 lPos = Camera.main.transform.InverseTransformPoint(_DebugLightOriginal.position);
        Vector3 pos = Camera.main.transform.TransformPoint(value - _FLA._LightErrorGrid.GetLength(0) / 2f, lPos.y, lPos.z);
        _DebugLightOriginal.position = pos;
    }

    private void onValueChangedDebugLightYo(float value)
    {
        Vector3 lPos = Camera.main.transform.InverseTransformPoint(_DebugLightOriginal.position);
        Vector3 pos = Camera.main.transform.TransformPoint(lPos.x, value - _FLA._LightErrorGrid.GetLength(1) / 2f, lPos.z);
        _DebugLightOriginal.position = pos;
    }

    private void onValueChangedDebugLightZo(float value)
    {
        Vector3 lPos = Camera.main.transform.InverseTransformPoint(_DebugLightOriginal.position);
        Vector3 pos = Camera.main.transform.TransformPoint(lPos.x, lPos.y, value - _FLA._LightErrorGrid.GetLength(2) / 2f);
        _DebugLightOriginal.position = pos;
    }


    private void onValueChangedDebugLightX(float value)
    {
        _debugLightPos.X = (int)value;
    }

    private void onValueChangedDebugLightY(float value)
    {
        _debugLightPos.Y = (int)value;
    }

    private void onValueChangedDebugLightZ(float value)
    {
        _debugLightPos.Z = (int)value;
    }

    private void onValueChangedDebugLights(bool value)
    {
        _debuggingLightPos = value;
    }

    private void Update()
    {
        if (Input.touchCount == 1)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase != TouchPhase.Began || UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(t.fingerId))
            {
                return;
            }
            if (!_realtime)
            {
                StartCoroutine(superpixelSegmentationRoutine());
            }
        }
        else if (Input.GetMouseButtonDown(1))
        {
            if (!_realtime)
            {
                StartCoroutine(superpixelSegmentationRoutine());

            }
        }

    }

    #region UI Events

    private void onValueChangedRealtime(bool value)
    {
        _realtime = value;
        if (!_realtime)
        {
            MessageManager._MessageManager.PushMessage("Tap screen to render superpixels.", 3);
            _SliderResDiv.maxValue = _sliderResLevelMax;
            _SliderClusterCount.maxValue = _sliderClusterCountMax;
        }
        else
        {
            _maxIterations = 1;
            _SliderResDiv.maxValue = _rSliderResLevelMax;
            _SliderClusterCount.maxValue = _rSliderClusterCountMax;
        }
    }

    private void onValueChangedSuperpixelMethod(bool value, SuperpixelMethod superpixelMethod)
    {
        if (!value)
        {
            _CurSuperpixelMethod = SuperpixelMethod.NONE;
            _PanelOptionsSLIC.gameObject.SetActive(false);
            _PanelOptionsWatershed.gameObject.SetActive(false);
            return;
        }

        _CurSuperpixelMethod = superpixelMethod;

        switch (superpixelMethod)
        {
            case SuperpixelMethod.SLIC:
                _PanelOptionsSLIC.gameObject.SetActive(true);
                _PanelOptionsWatershed.gameObject.SetActive(false);
                break;
            case SuperpixelMethod.CWATERSHED:
                _PanelOptionsSLIC.gameObject.SetActive(false);
                _PanelOptionsWatershed.gameObject.SetActive(true);
                break;
            default:
                break;
        }
    }

    private void onValueChangedScreenMode(ScreenMode screenMode)
    {
        _CurScreenMode = screenMode;

        switch (screenMode)
        {
            case ScreenMode.MESH3D:
                break;
            case ScreenMode.ALBEDO:
                break;
            case ScreenMode.ERROR:
                break;
            case ScreenMode.RESULT:
                break;
            default:
                break;
        }
    }

    private void onValueChangedResDiv(float value)
    {
        _ResDiv = (int)(8 - value) + 8;

        TangoCameraIntrinsics intrinsics = new TangoCameraIntrinsics();
        VideoOverlayProvider.GetIntrinsics(TangoEnums.TangoCameraId.TANGO_CAMERA_COLOR, intrinsics);
        _ErrorTexture = new Texture2D((int)(intrinsics.width / (float)_ResDiv), (int)(intrinsics.height / (float)_ResDiv), TextureFormat.RGBA32, false);
        _ErrorTexture.filterMode = FilterMode.Point;
        _ErrorTexture.anisoLevel = 0;

        _AlbedoTexture = new Texture2D((int)(1280 / (float)_ResDiv), (int)(720 / (float)_ResDiv));
        _AlbedoTexture.filterMode = FilterMode.Point;
        _AlbedoTexture.mipMapBias = 0;

        _Cam3DMesh.targetTexture = new RenderTexture((int)(1280 / (float)_ResDiv), (int)(720 / (float)_ResDiv), 16, RenderTextureFormat.ARGB32);
        _Cam3DMesh.targetTexture.filterMode = FilterMode.Bilinear;
        _Cam3DMesh.targetTexture.mipMapBias = 0;
    }

    private void onValueChangedClusterCount(float value)
    {
        _ClusterCount = (int)value;
        _SLIC._ClusterCount = _ClusterCount;
        _Watershed._ClusterCount = _ClusterCount;
    }

    private void onValueChangedMaxIterations(float value)
    {
        _maxIterations = (int)value;
        _SLIC.MaxIterations = _maxIterations;
    }

    private void onValueChangedBorderThreshold(float value)
    {
        _BorderThreshold = (int)value;
        _Watershed._BorderThreshold = _BorderThreshold;
    }

    private void onValueChangedCompactness(float value)
    {
        _Compactness = (int)value;
        _SLIC.Compactness = _Compactness;
    }

    #endregion

    private void drawSuperPixels(ref List<Superpixel> superpixels)
    {
        foreach (Superpixel s in superpixels)
        {
            Color oC = new Color();
            Color iC = new Color();
            float ns = 0;
            float albedo = 0;
            float Ir = 0;
            float Io = s.Intensity / 255f;
            Vector3 lightDir = ImageProcessing.LightDirection(_FLA._EstimatedLightPos, s.WorldPoint);

            ImageProcessing.ComputeAlbedo(Io, s.Normal, lightDir, out ns, out albedo);
            if (ns > 0)
            {
                if (albedo > 2.5)
                {
                    oC = Color.red;
                    iC = Color.red;
                }
                else
                {
                    ImageProcessing.ComputeImageIntensity(albedo, s.Normal, lightDir, out Ir);
                    oC = new Color(Ir, Ir, Ir);
                    iC = new Color(albedo, albedo, albedo);
                }
            }
            else
            {
                oC = new Color(Ir, Ir, Ir);
                iC = Color.cyan;
            }

            foreach (RegionPixel p in s.Pixels)
            {
                if (Io - Ir == 0)
                {
                    oC = Color.black;
                }
                else
                {
                    oC = new Color(1, 1, 1);
                }

                _AlbedoTexture.SetPixel(p.X, _ErrorTexture.height - p.Y, iC);
                _ErrorTexture.SetPixel(p.X, _ErrorTexture.height - p.Y, oC);
            }

        }

        _ErrorTexture.Apply();
        _AlbedoTexture.Apply();
    }

    private void drawRegionPixels(ref List<RegionPixel> rpixels)
    {
        foreach (RegionPixel r in rpixels)
        {
            Color oC = new Color();
            Color iC = new Color();

            float ns = 0;
            float albedo = 0;
            float Ir = 0;
            float Io = r.Intensity / 255f;
            Vector3 lightDir = ImageProcessing.LightDirection(_FLA._EstimatedLightPos, r.WorldPoint);

            ImageProcessing.ComputeAlbedo(Io, r.Normal, lightDir, out ns, out albedo);
            if (ns > 0)
            {
                if (albedo > 2.5)
                {
                    oC = Color.red;
                    iC = Color.red;
                }
                else
                {
                    ImageProcessing.ComputeImageIntensity(albedo, r.Normal, lightDir, out Ir);
                    oC = new Color(Ir, Ir, Ir);
                    iC = new Color(albedo, albedo, albedo);
                }
            }
            else
            {
                oC = new Color(Ir, Ir, Ir);
                iC = Color.cyan;
            }

            if (Io - Ir == 0)
            {
                oC = Color.black;
            }
            else
            {
                oC = new Color(1, 1, 1);
            }

            //oC = ImageProcessing.Vector3ToColor(r.Normal);

            _AlbedoTexture.SetPixel(r.X, _ErrorTexture.height - r.Y, iC);
            _ErrorTexture.SetPixel(r.X, _ErrorTexture.height - r.Y, oC);
        }
    }


    private void doCompactWatershed()
    {
        //Vector3[,] pixels = TangoHelpers.ImageBufferToArray(_lastImageBuffer, (uint)_ResDiv, true);
        Vector3[,] pixels = ImageProcessing.RenderTextureToRGBArray(_Cam3DMesh.targetTexture);

        //pixels = ImageProcessing.MedianFilter3x3(ref pixels);
        List<Superpixel> superpixels;
        int[,] S = _Watershed.Run(pixels, out superpixels);

        foreach (Superpixel s in superpixels)
        {
            s.ComputeImageIntensity();
            s.ComputeSurfaceNormal(_ErrorTexture.width, _ErrorTexture.height);
        }

        if (_debuggingLightPos)
        {
            Vector3 lightPos = Camera.main.transform.TransformPoint(
                _debugLightPos.X - _FLA._LightErrorGrid.GetLength(0) / 2f,
                _debugLightPos.Y - _FLA._LightErrorGrid.GetLength(1) / 2f,
                _debugLightPos.Z - _FLA._LightErrorGrid.GetLength(2) / 2f);
            _FLA._EstimatedLightPos = lightPos;

            float[] Io = new float[superpixels.Count];
            for (int i = 0; i < superpixels.Count; i++)
            {
                Io[i] = superpixels[i].Intensity;
            }

            float error = FindLightAdvanced.IoIrL2Norm(ref superpixels, Io, lightPos, _ErrorTexture.width, _ErrorTexture.height);
            Debug.Log("LightPos: " + lightPos + " error: " + error);
        }
        else
        {
            _FLA._EstimatedLightPos = _FLA.LightEstimation(ref superpixels, _ErrorTexture.width, _ErrorTexture.height);
        }


        //Debug.DrawRay(_estimatedLightPos, Camera.main.transform.position - _estimatedLightPos, Color.magenta);
        _DebugPointLight.transform.position = _FLA._EstimatedLightPos;
        //_DebugPointLight.transform.LookAt(_DebugLightReceiver);

        drawSuperPixels(ref superpixels);
        //_ResultMat.mainTexture = _ErrorTexture;
        //_IoMat.mainTexture = _AlbedoTexture;
    }

    private void doSLIC()
    {
        //Vector3[,] pixels = TangoHelpers.ImageBufferToArray(_lastImageBuffer, (uint)_ResDiv, true);
        Vector3[,] pixels = ImageProcessing.RenderTextureToRGBArray(_Cam3DMesh.targetTexture);

        //pixels = ImageProcessing.MedianFilter3x3(ref pixels);
        List<Superpixel> superpixels;
        List<CIELABXYCenter> clusterCenters = _SLIC.RunSLICSegmentation(pixels, out superpixels);

        foreach (Superpixel s in superpixels)
        {
            s.ComputeImageIntensity();
            s.ComputeSurfaceNormal(_ErrorTexture.width, _ErrorTexture.height);
        }

        if (_debuggingLightPos)
        {
            Vector3 lightPos = Camera.main.transform.TransformPoint(
                _debugLightPos.X - _FLA._LightErrorGrid.GetLength(0) / 2f,
                _debugLightPos.Y - _FLA._LightErrorGrid.GetLength(1) / 2f,
                _debugLightPos.Z - _FLA._LightErrorGrid.GetLength(2) / 2f);
            _FLA._EstimatedLightPos = lightPos;

            float[] Io = new float[superpixels.Count];
            for (int i = 0; i < superpixels.Count; i++)
            {
                Io[i] = superpixels[i].Intensity / 255f;
            }

            float error = FindLightAdvanced.IoIrL2Norm(ref superpixels, Io, lightPos, _ErrorTexture.width, _ErrorTexture.height);
            Debug.Log("LightPos: " + lightPos + " error: " + error);
        }
        else
        {
            _FLA._EstimatedLightPos = _FLA.LightEstimation(ref superpixels, _ErrorTexture.width, _ErrorTexture.height);
        }
        _DebugPointLight.transform.position = _FLA._EstimatedLightPos;
        //_DebugPointLight.transform.LookAt(_DebugLightReceiver);

        drawSuperPixels(ref superpixels);
        //_ResultMat.mainTexture = _ErrorTexture;
        //_IoMat.mainTexture = _AlbedoTexture;
    }

    private VectorInt2 ShortestEdge(ref List<Vector3> verts, VectorInt2[] edges)
    {
        VectorInt2 result = new VectorInt2();

        float minDist = float.MaxValue;
        for (int i = 0; i < edges.Length; i++)
        {
            float cur = Vector3.Distance(verts[edges[i].X], verts[edges[i].Y]);
            
            if (cur < minDist)
            {
                minDist = cur;
                result = new VectorInt2(edges[i]);
            }
            //Debug.Log("(" + i + ") " + cur + " result: " + result.ToString());
        }

        return result;
    }

    private List<Vector3> candidateLightDirections(Vector3[,] negX, Vector3[,] posX,
        Vector3[,] negY, Vector3[,] posY,
        Vector3[,] negZ, Vector3[,] posZ,
        Vector3 camPos)
    {
        List<Vector3> candidateDirections = new List<Vector3>(6);

        Vector2 b = new Vector2();
        Vector3 dir = new Vector3();

        // Positive directions.
        b = ImageProcessing.BrightestPoint(posX);
        posX[(int)b.x, (int)b.y] = ImageProcessing.ColorToVector3(Color.red);
        dir = new Vector3(
            0.5f,
            b.y / _cubeMap.height - 0.5f,
            (1f - b.x / _cubeMap.width) - 0.5f) - camPos;
        dir.Normalize();
        candidateDirections.Add(dir);

        b = ImageProcessing.BrightestPoint(posY);
        posY[(int)b.x, (int)b.y] = ImageProcessing.ColorToVector3(Color.green);
        dir = new Vector3(
            b.x / _cubeMap.width - 0.5f,
            0.5f,
            (1f - b.y / _cubeMap.height) - 0.5f) - camPos;
        dir.Normalize();
        candidateDirections.Add(dir);

        b = ImageProcessing.BrightestPoint(posZ);
        posZ[(int)b.x, (int)b.y] = ImageProcessing.ColorToVector3(Color.blue);
        dir = new Vector3(
            b.x / _cubeMap.width - 0.5f,
            b.y / _cubeMap.height - 0.5f,
            0.5f) - camPos;
        dir.Normalize();
        candidateDirections.Add(dir);


        // Negative directions.
        b = ImageProcessing.BrightestPoint(negX);
        negX[(int)b.x, (int)b.y] = ImageProcessing.ColorToVector3(Color.red);
        dir = new Vector3(
            -0.5f,
            b.y / _cubeMap.height - 0.5f,
            b.x / _cubeMap.width - 0.5f) - camPos;
        dir.Normalize();
        candidateDirections.Add(dir);

        b = ImageProcessing.BrightestPoint(negY);
        negY[(int)b.x, (int)b.y] = ImageProcessing.ColorToVector3(Color.green);
        dir = new Vector3(
            b.x / _cubeMap.width - 0.5f,
            -0.5f,
            b.y / _cubeMap.height - 0.5f) - camPos;
        dir.Normalize();
        candidateDirections.Add(dir);

        b = ImageProcessing.BrightestPoint(negZ);
        negZ[(int)b.x, (int)b.y] = ImageProcessing.ColorToVector3(Color.blue);
        dir = new Vector3(
            (1f - b.x / _cubeMap.width) - 0.5f,
            b.y / _cubeMap.height - 0.5f,
            -0.5f) - camPos;
        dir.Normalize();
        candidateDirections.Add(dir);


        //Debug.DrawRay(camPos, dir * 5, Color.cyan, 10f);
        //Debug.Log(b + " " + dir);
        return candidateDirections;
    }

    public Cubemap _cubeMap;
    private void doLightEstimationCubemap()
    {

        Vector3[,] pixels = ImageProcessing.RenderTextureToRGBArray(_Cam3DMesh.targetTexture);
        List<RegionPixel> rpixels = RegionPixel.ToRegionPixels(pixels);
        foreach (RegionPixel r in rpixels)
        {
            r.ComputeImageIntensity();
            r.ComputeSurfaceNormal(_ErrorTexture.width, _ErrorTexture.height);
        }

        GameObject go = new GameObject("CubemapCam");
        go.AddComponent<Camera>();
        go.transform.position = Camera.main.transform.position;
        go.transform.rotation = Camera.main.transform.rotation;
        go.GetComponent<Camera>().cullingMask = Camera.main.cullingMask;

        if (!go.GetComponent<Camera>().RenderToCubemap(_cubeMap))
        {
            DestroyImmediate(go);
            return;
        }

        Vector3[,] negX = ImageProcessing.CubemapFaceTo2DVector3Array(_cubeMap, CubemapFace.NegativeX);
        Vector3[,] posX = ImageProcessing.CubemapFaceTo2DVector3Array(_cubeMap, CubemapFace.PositiveX);
        Vector3[,] negZ = ImageProcessing.CubemapFaceTo2DVector3Array(_cubeMap, CubemapFace.NegativeZ);
        Vector3[,] posZ = ImageProcessing.CubemapFaceTo2DVector3Array(_cubeMap, CubemapFace.PositiveZ);
        Vector3[,] negY = ImageProcessing.CubemapFaceTo2DVector3Array(_cubeMap, CubemapFace.NegativeY);
        Vector3[,] posY = ImageProcessing.CubemapFaceTo2DVector3Array(_cubeMap, CubemapFace.PositiveY);

        List<Vector3> lightDirs = candidateLightDirections(negX, posX, negY, posY, negZ, posZ, go.transform.position);

        VectorInt2[] edges = 
        {
            new VectorInt2(0,1), new VectorInt2(0,2), new VectorInt2(0,4), new VectorInt2(0,5),
            new VectorInt2(1,2), new VectorInt2(1,3), new VectorInt2(1,5),
            new VectorInt2(2,3), new VectorInt2(2,4),
            new VectorInt2(3,4), new VectorInt2(3,5),
            new VectorInt2(4,5)
        };
        VectorInt2 sEdge = ShortestEdge(ref lightDirs, edges);
        Debug.DrawRay(lightDirs[sEdge.X]*10, (lightDirs[sEdge.Y] - lightDirs[sEdge.X]).normalized *5* Vector3.Distance(lightDirs[sEdge.Y], lightDirs[sEdge.X]), Color.black);

        Debug.DrawRay(go.transform.position, lightDirs[0] * 5, Color.red);
        Debug.DrawRay(go.transform.position, lightDirs[1] * 5, Color.green);
        Debug.DrawRay(go.transform.position, lightDirs[2] * 5, Color.blue);
        Debug.DrawRay(go.transform.position, lightDirs[3] * 5, Color.red);
        Debug.DrawRay(go.transform.position, lightDirs[4] * 5, Color.green);
        Debug.DrawRay(go.transform.position, lightDirs[5] * 5, Color.blue);

        for (int i = 0; i < lightDirs.Count; i++)
        {
            lightDirs[i] *= 5f;
        }


        //_FLA._EstimatedLightPos = _FLA.LightEstimation(ref rpixels, _ErrorTexture.width, _ErrorTexture.height, ref lightDirs);
        //_DebugPointLight.transform.position = _FLA._EstimatedLightPos;
        _DebugPointLight.transform.position = (lightDirs[sEdge.X] + lightDirs[sEdge.Y]) / 2f;

        //for (int i = 0; i < _AlbedoTexture.width; i++)
        //{
        //    for (int j = 0; j < _AlbedoTexture.height; j++)
        //    {
        //        int x = (int)(i * (_cubeMap.width / (float)_AlbedoTexture.width));
        //        int y = (int)(j * (_cubeMap.height / (float)_AlbedoTexture.height));
        //        _AlbedoTexture.SetPixel(i, j, ImageProcessing.Vector3ToColor( posZ[x, y]));
        //    }
        //}

        //_RawImageScreen.texture = _AlbedoTexture;
        //_AlbedoTexture.Apply();

        switch (_CurScreenMode)
        {
            case ScreenMode.MESH3D:
                _RawImageScreen.texture = _Mesh3DTexture;
                break;
            case ScreenMode.ALBEDO:
                _RawImageScreen.texture = _AlbedoTexture;
                _AlbedoTexture.Apply();
                break;
            case ScreenMode.ERROR:
                _RawImageScreen.texture = _ErrorTexture;
                _ErrorTexture.Apply();
                break;
            case ScreenMode.RESULT:
                _RawImageScreen.texture = _InTexture;
                break;
            default:
                break;
        }


        DestroyImmediate(go);
    }

    private void doLightEstimation()
    {
        //Vector3[,] pixels = TangoHelpers.ImageBufferToArray(_lastImageBuffer, (uint)_ResDiv, true);
        Vector3[,] pixels = ImageProcessing.RenderTextureToRGBArray(_Cam3DMesh.targetTexture);

        //pixels = ImageProcessing.MedianFilter3x3(ref pixels);
        //float[,] edges = ImageProcessing.SobelFilter3x3(ref pixels, true);

        List<RegionPixel> rpixels = RegionPixel.ToRegionPixels(pixels);
        foreach (RegionPixel r in rpixels)
        {
            r.ComputeImageIntensity();
            r.ComputeSurfaceNormal(_ErrorTexture.width, _ErrorTexture.height);
        }

        if (_debuggingLightPos)
        {

            Vector3 lightPos = Camera.main.transform.TransformPoint(
                _debugLightPos.X - _FLA._LightErrorGrid.GetLength(0) / 2f,
                _debugLightPos.Y - _FLA._LightErrorGrid.GetLength(1) / 2f,
                _debugLightPos.Z - _FLA._LightErrorGrid.GetLength(2) / 2f);

            _FLA._EstimatedLightPos = lightPos;

            float[] Io = new float[rpixels.Count];
            for (int i = 0; i < rpixels.Count; i++)
            {
                Io[i] = rpixels[i].Intensity / 255f;
            }

            float error = FindLightAdvanced.IoIrL2Norm(ref rpixels, Io, lightPos, _ErrorTexture.width, _ErrorTexture.height);
            Debug.Log("LightPos: " + lightPos + " error: " + error);
        }
        else
        {
            _FLA._EstimatedLightPos = _FLA.LightEstimation(ref rpixels, _ErrorTexture.width, _ErrorTexture.height);
        }
        _DebugPointLight.transform.position = _FLA._EstimatedLightPos;
        //_DebugPointLight.transform.LookAt(_DebugLightReceiver);

        drawRegionPixels(ref rpixels);
        switch (_CurScreenMode)
        {
            case ScreenMode.MESH3D:
                _RawImageScreen.texture = _Mesh3DTexture;
                break;
            case ScreenMode.ALBEDO:
                _RawImageScreen.texture = _AlbedoTexture;
                _AlbedoTexture.Apply();
                break;
            case ScreenMode.ERROR:
                _RawImageScreen.texture = _ErrorTexture;
                _ErrorTexture.Apply();
                break;
            case ScreenMode.RESULT:
                _RawImageScreen.texture = _InTexture;
                break;
            default:
                break;
        }
    }

    private void superpixelSegmentation()
    {
        doLightEstimationCubemap();
        return;
        switch (_CurSuperpixelMethod)
        {
            case SuperpixelMethod.SLIC:
                doSLIC();
                break;
            case SuperpixelMethod.CWATERSHED:
                doCompactWatershed();
                break;
            case SuperpixelMethod.NONE:
                doLightEstimation();
                break;
            default:
                break;
        }
    }

    private IEnumerator superpixelSegmentationRoutine()
    {
        MessageManager._MessageManager.PushMessage("Performing Superpixel Segmentation ...");
        yield return null;
        superpixelSegmentation();
    }

    #region Tango Events

    public void OnTangoImageAvailableEventHandler(TangoEnums.TangoCameraId cameraId, TangoUnityImageData imageBuffer)
    {
        _lastImageBuffer = imageBuffer;

        if (_realtime)
        {
            superpixelSegmentation();
        }

    }

    public void OnTangoPermissions(bool permissionsGranted)
    {
#if UNITY_EDITOR
        _ErrorTexture = new Texture2D(1280 / _ResDiv, 720 / _ResDiv, TextureFormat.ARGB32, false);
        _ErrorTexture.filterMode = FilterMode.Point;
        _ErrorTexture.anisoLevel = 0;
#else
        TangoCameraIntrinsics intrinsics = new TangoCameraIntrinsics();
        VideoOverlayProvider.GetIntrinsics(TangoEnums.TangoCameraId.TANGO_CAMERA_COLOR, intrinsics);
        _ErrorTexture = new Texture2D((int)(intrinsics.width / _ResDiv), (int)(intrinsics.height / _ResDiv), TextureFormat.RGBA32, false);
        _ErrorTexture.filterMode = FilterMode.Point;
        _ErrorTexture.anisoLevel = 0;
#endif

        Debug.Log(_ErrorTexture.width + "x" + _ErrorTexture.height);
    }

    public void OnTangoServiceConnected()
    {
        //_tangoApplication.SetDepthCameraRate(TangoEnums.TangoDepthCameraRate.DISABLED);
    }

    public void OnTangoServiceDisconnected()
    {
    }

    #endregion

}
