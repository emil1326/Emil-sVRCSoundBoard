#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;

public class Builder : MonoBehaviour
{
    [Header("Settings")]
    public List<AudioClip> AudioClipList;
    public List<Texture2D> PlaceHolderImage;
    public bool AllowOthersToInteract = false;
    public string MainPath = "Assets\\Emil'sAssets";

    [Header("Building")]
    public bool Build = false;
    public bool finishSetUp = false;

    [Header("Individual tests")]
    public bool TestAudio = false;
    public bool TestImage = false;
    public bool FullTestImage = false;
    public bool TestAnimateBakedImages = false;
    public bool TestSetAnimator = false;
    public bool TestSetObject = false;
    public bool CheckPaths = false;
    public bool CleanUP = false;

    [Header("Advanced settings")]
    public AnimatorController AC;
    public AnimationClip MatPageClip;
    public AnimationClip AudioClip;
    public int BakedImageSize = 4096;
    public bool AllowDestroyOfScriptOnFinish = true;
    public bool UseAutoPath = true;
    public string TextureDataPath = "Assets\\Emil'sAssets\\Textures\\Baked";
    public string AudioClipDataPath = "Assets\\Emil'sAssets\\Animations\\Anim";
    public string MaterialDataPath = "Assets\\Emil'sAssets\\mat\\BakedShaders";
    private string OldPath = "Assets\\Emil'sAssets";
    public float DividableMeshRatio = 3.0f;
    public List<Texture2D> BakedImages = new();
    public List<Material> BakedMaterials = new();
    public Shader MainShader;
    public GameObject AudioclipEmplacement;
    public AudioSource[] audioSources;
    public Material MainMaterial;
    public GameObject Pannel;
    public Vector2Int AnimatorGenOffset = new(400, 400);
    private List<Object> DestroyList = new();

    private void OnValidate()
    {
        EditorUtility.ClearProgressBar();

        PreLoad();

        if (UseAutoPath)
            ChangePath();
        if (Build)
            BuildIt();
        if (TestAudio)
            DoAudio();
        if (TestImage)
            DoImages(0);
        if (FullTestImage)
            for (int i = 0; i < Mathf.CeilToInt(PlaceHolderImage.Count / 12f); i++)
                DoImages(i);
        if (TestAnimateBakedImages)
            AnimateBakedImages();
        if (TestSetAnimator)
            SetAnimatorStuff();
        if (TestSetObject)
            SetObject();
        if (CleanUP)
            Cleanup();
        if (finishSetUp)
            FinishSetUp();

        AssetDatabase.Refresh();

        if (CheckPaths)
            if (!CheckFilePathOKVerbose())
                EditorUtility.DisplayDialog("Error", "Missing path, please check that they are properly set.", "Ok");
    }

    void PreLoad()
    {
        if (AC != null)
        {
            MatPageClip = (AnimationClip)GetStateByName("MaterialPage", AC.layers[2].stateMachine).motion;
            AudioClip = (AnimationClip)GetStateByName("Wait", AC.layers[3].stateMachine).motion;
        }
        if (MainMaterial != null)
            MainShader = MainMaterial.shader;
    }

    void ChangePath()
    {
        TextureDataPath = TextureDataPath[OldPath.Length..];
        TextureDataPath = MainPath + TextureDataPath;
        AudioClipDataPath = AudioClipDataPath[OldPath.Length..];
        AudioClipDataPath = MainPath + AudioClipDataPath;
        MaterialDataPath = MaterialDataPath[OldPath.Length..];
        MaterialDataPath = MainPath + MaterialDataPath;
        OldPath = MainPath;
    }

    void BuildIt()
    {
        Build = false;

        if (!CheckFilePathOK())
        {
            EditorUtility.DisplayDialog("Error", "Missing path, please check that they are properly set.\nAborting the procedure", "Ok");
            return;

        }

        if (PlaceHolderImage.Count == 0 || AudioClipList.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "Missing Images or Clips.\nAborting the procedure", "Ok");
            return;
        }

        Cleanup();

        for (int i = 0; i < Mathf.CeilToInt(PlaceHolderImage.Count / 12f); i++)
        {
            DoImages(i); // progress bar is inside
        }

        EditorUtility.DisplayProgressBar("Building stuff", "making Animation clips ", 0.97f);

        AnimateBakedImages();

        EditorUtility.DisplayProgressBar("Building stuff", "Setting the AudioClips ", 0.98f);

        DoAudio();

        EditorUtility.DisplayProgressBar("Building stuff", "Finishing up ", 0.99f);

        SetAnimatorStuff();

        SetObject();

        ReloadPoiShader();

        EditorUtility.ClearProgressBar();

        AssetDatabase.Refresh();
    }

    void Cleanup()
    {
        CleanUP = false;

        foreach (var image in BakedImages)
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(image));

        BakedImages.Clear();

        foreach (GameObject GM in GetAllChildrensOf(AudioclipEmplacement))
            DestroyList.Add(GM);

        DeferedDestroy();

        audioSources = new AudioSource[0];

        MatPageClip.ClearCurves();
        AudioClip.ClearCurves();

        RemoveOldStates();

        foreach (Material M in BakedMaterials)
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(M));

        BakedMaterials.Clear();

        AssetDatabase.Refresh();
    }

    bool CheckFilePathOK()
    {
        CheckPaths = false;
        if (AssetDatabase.IsValidFolder(MainPath))
            if (AssetDatabase.IsValidFolder(TextureDataPath))
                if (AssetDatabase.IsValidFolder(AudioClipDataPath))
                    if (AssetDatabase.IsValidFolder(MaterialDataPath))
                        return true;
        return false;
    }

    bool CheckFilePathOKVerbose()
    {
        CheckPaths = false;
        if (AssetDatabase.IsValidFolder(MainPath))
        {
            Debug.Log("MainPathValid");
            if (AssetDatabase.IsValidFolder(TextureDataPath))
            {
                Debug.Log("TexturePathValid");
                if (AssetDatabase.IsValidFolder(AudioClipDataPath))
                {
                    Debug.Log("AudioClipPathValid");
                    if (AssetDatabase.IsValidFolder(MaterialDataPath))
                    {
                        Debug.Log("MaterialPathValid");
                        return true;
                    }
                }
            }
        }
        return false;
    }

    void FinishSetUp()
    {
        finishSetUp = false;

        foreach (Texture2D texture in BakedImages)
        {
            string texturePath = AssetDatabase.GetAssetPath(texture);
            TextureImporter textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (textureImporter != null)
            {
                textureImporter.isReadable = true;
                textureImporter.mipmapEnabled = true;
                textureImporter.streamingMipmaps = true;
                textureImporter.ignoreMipmapLimit = true;
                textureImporter.alphaIsTransparency = true;
                textureImporter.filterMode = FilterMode.Trilinear;
                AssetDatabase.ImportAsset(texturePath);
            }

            texture.Apply(true);
        }

        for (int i = 0; i < AudioClipList.Count; i++)
            ConfigureImportSettings(AssetDatabase.GetAssetPath(AudioClipList[i]));
        EditorUtility.DisplayDialog("Thank you", "Thank you for using Emil's Soundboard builder! Enjoy! \nIf you need any help => emil1326_7742 (discord)", "Ok");
        if (AllowDestroyOfScriptOnFinish)
            DestroyList.Add(this);
        DeferedDestroy();
    }

    void SetAnimatorStuff()
    {
        TestSetAnimator = false;

        RemoveOldStates();

        AnimatorStateMachine stateMachine3 = AC.layers[3].stateMachine;

        AnimatorStateMachine stateMachine1 = AC.layers[1].stateMachine;

        AnimatorState RootState = GetStateByName("Wait", stateMachine3);

        if (RootState == null)
        {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Error", "There is a missing state in the animator layer 3, please only use the given animator", "ok");
            return;
        }
        else
        {
            int Done = 0;
            for (int Pages = 0; Pages < BakedImages.Count; Pages++)
                for (int i = 0; i < 4; i++)
                    for (int j = 0; j < 3; j++)
                    {
                        if (Done >= PlaceHolderImage.Count)
                            break;
                        Done++;

                        AnimatorState Curranimstate = stateMachine3.AddState($"{i},{j}@P {Pages}", CalculateGridPosition(Pages, i, j, BakedImages.Count, 220, 50, AnimatorGenOffset.x, AnimatorGenOffset.y));

                        AnimatorStateTransition CurrAST = RootState.AddTransition(Curranimstate);
                        CurrAST.AddCondition(AnimatorConditionMode.If, 1, $"ES/{i}{j}");
                        CurrAST.AddCondition(AnimatorConditionMode.Equals, Pages, $"ES/Page");
                        CurrAST.hasExitTime = false;
                        CurrAST.exitTime = 0;
                        CurrAST.duration = 0.02f;
                        Curranimstate.motion = AudioClip;
                        Curranimstate.speed = 0;
                        Curranimstate.cycleOffset = ((1f - 0.0001f) / (PlaceHolderImage.Count + 1f)) * Done;
                        AddRetunTransitions(Curranimstate, RootState);
                    }
        }

        AnimatorStateTransition[] AllTransitions = GetAllAST(stateMachine1);
        foreach (AnimatorStateTransition Transition in AllTransitions)
            if (Transition.name == "ResetHigh")
            {
                Transition.AddCondition(Transition.conditions[0].mode, BakedImages.Count - 1, Transition.conditions[0].parameter);
                Transition.RemoveCondition(Transition.conditions[0]);
            }
            else if (Transition.name == "ResetHigh2")
            {
                Transition.AddCondition(Transition.conditions[1].mode, BakedImages.Count, Transition.conditions[1].parameter);
                Transition.RemoveCondition(Transition.conditions[1]);
            }

        AnimatorState VRCConverterState = GetStateByName("Set", stateMachine1);

        VRCAvatarParameterDriver MSBehavior = (VRCAvatarParameterDriver)VRCConverterState.behaviours[0];

        MSBehavior.parameters[0].sourceMax = BakedImages.Count - 1;

        VRCConverterState.behaviours[0] = MSBehavior;
    }

    void AnimateBakedImages()
    {
        TestAnimateBakedImages = false;

        if (MatPageClip == null)
            MatPageClip = new();

        List<Material> materials = new();

        for (int i = 0; i < BakedImages.Count; i++)
        {
            Material newMat = CreateMatFromWithImage(MainMaterial, BakedImages[i]);
            AssetDatabase.CreateAsset(newMat, MaterialDataPath + $"\\MatPageGen_{i}.mat");
            materials.Add(newMat);
        }

        BakedMaterials = materials;

        MatPageClip.wrapMode = WrapMode.Once;

        ObjectReferenceKeyframe[] keyFrames = new ObjectReferenceKeyframe[materials.Count + 1];

        for (int i = 0; i < materials.Count; i++)
        {
            keyFrames[i] = new ObjectReferenceKeyframe
            {
                time = i,
                value = materials[i]
            };
        }

        keyFrames[materials.Count] = new ObjectReferenceKeyframe
        {
            time = materials.Count,
            value = materials[^1]
        };

        EditorCurveBinding curveBinding = new()
        {
            type = typeof(MeshRenderer),
            path = FindHierarchyPathFromParent(gameObject, Pannel),
            propertyName = "m_Materials.Array.data[0]"
        };

        AnimationUtility.SetObjectReferenceCurve(MatPageClip, curveBinding, keyFrames);

        if (!AssetDatabase.Contains(MatPageClip))
            AssetDatabase.CreateAsset(MatPageClip, MaterialDataPath + "\\MatPageClip.anim");

    }

    void DoImages(int Index)
    {
        TestImage = false;
        FullTestImage = false;
        Texture2D Render = new(BakedImageSize, BakedImageSize);
        Color32[] transparentPixels = new Color32[BakedImageSize * BakedImageSize];
        for (int i = 0; i < transparentPixels.Length; i++)
        {
            transparentPixels[i] = new Color32(0, 0, 0, 0); // Transparent black color
        }
        Render.SetPixels32(transparentPixels);
        // s @ 144 x + 200 4
        // S @ 124 y + 30  3 
        int j = Index * 12;

        for (int x = 0; x < 4; x++)
            for (int y = 0; y < 3; y++)
            {
                if (j >= PlaceHolderImage.Count)
                    break;

                int posX = 144 + (x * (253 + 625));
                int posY = (BakedImageSize - 120 - 625) - (y * (130 + 625));

                Texture2D currentImage = PlaceHolderImage[j];

                Texture2D resizedImage = Resize(currentImage, 625, 625);

                Color32[] newTPixels = resizedImage.GetPixels32();
                Render.SetPixels32(posX, posY, 625, 625, newTPixels);
                Render.Apply();

                j++;

                EditorUtility.DisplayProgressBar("Building stuff", "Making images " + Index + j, (Index + j - 0.1f) / PlaceHolderImage.Count);
            }

        string textureFilePath = $"{TextureDataPath}/Baked{Index}.png";
        byte[] bytes = Render.EncodeToPNG();
        File.WriteAllBytes(textureFilePath, bytes);
        AssetDatabase.Refresh();
        BakedImages.Add(LoadTextureFromFile(textureFilePath));
    }

    void DoAudio()
    {
        TestAudio = false;
        audioSources = new AudioSource[AudioClipList.Count];

        for (int i = 0; i < AudioClipList.Count; i++)
        {
            GameObject audioObject = new("AudioSource_" + i);
            audioObject.transform.SetParent(AudioclipEmplacement.transform); // Set the parent of the GameObject to this object
            audioSources[i] = audioObject.AddComponent<AudioSource>(); // Add an AudioSource component to the GameObject

            audioSources[i].clip = AudioClipList[i];
            audioSources[i].playOnAwake = true;

            audioSources[i].gameObject.SetActive(false);
        }

        CreateAnimation();
    }

    void CreateAnimation()
    {
        AudioClip.wrapMode = WrapMode.Once;

        for (int i = 0; i < AudioClipList.Count; i++) // do all the curves
        {
            AnimationCurve curve = new();
            if (i == 0)
                curve.AddKey(0, 1);
            else
                curve.AddKey(0, 0);

            for (int j = 1; j < AudioClipList.Count + 2; j++) // do all the keyframes on each curves
            {
                if (i == j)
                    curve.AddKey(j, 1);
                else
                    curve.AddKey(j, 0);
            }

            curve.AddKey(AudioClipList.Count, 1);

            for (int j = 1; j <= AudioClipList.Count; j++)
            {
                AnimationUtility.SetKeyBroken(curve, j, false);
                AnimationUtility.SetKeyLeftTangentMode(curve, j, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(curve, j, AnimationUtility.TangentMode.Constant);
            }

            AudioClip.SetCurve(FindHierarchyPathFromParent(gameObject, audioSources[i].transform.gameObject), typeof(GameObject), "m_IsActive", curve);
        }

        if (!AssetDatabase.Contains(AudioClip))
            AssetDatabase.CreateAsset(AudioClip, AudioClipDataPath + "\\AudioClipAnim.anim");
    }

    private void ConfigureImportSettings(string filePath)
    {
        AudioImporter importer = AssetImporter.GetAtPath(filePath) as AudioImporter;

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;

        settings.loadType = AudioClipLoadType.CompressedInMemory; //better mem usage for large files
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 100;
        settings.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;

        importer.defaultSampleSettings = settings;

        AssetDatabase.ImportAsset(filePath);
    }

    void ReloadPoiShader()
    {
        foreach (Material M in BakedMaterials)
        {
            M.shader = MainShader;
        }
    }

    void SetObject()
    {
        TestSetObject = false;
        float HighestScale = GetHighestChildren(gameObject.transform.parent.gameObject);
        Pannel.transform.localScale = new Vector3(HighestScale / DividableMeshRatio, HighestScale / DividableMeshRatio, HighestScale / DividableMeshRatio) * 50;

        foreach (GameObject GM in GetAllChildrensOf(Pannel.transform.Find("Buttons").gameObject))
            GM.GetComponent<VRCContactReceiver>().allowOthers = AllowOthersToInteract;
    }

    float GetHighestChildren(GameObject Root)
    {
        float Currmaxheight = 0;
        for (int i = 0; i < Root.transform.childCount; i++)
            if (Root.transform.GetChild(i).TryGetComponent<SkinnedMeshRenderer>(out var SKMR))
            {
                float MaxHeight = SKMR.bounds.max.y;
                if (MaxHeight > Currmaxheight)
                    Currmaxheight = MaxHeight;
            }

        return Currmaxheight;
    }

    AnimatorStateTransition[] GetAllAST(AnimatorStateMachine Source)
    {
        List<AnimatorStateTransition> All = new();
        for (int i = 0; i < Source.states.Length; i++)
            All.AddRange(Source.states[i].state.transitions);

        return All.ToArray();
    }

    void RemoveOldStates()
    {
        AnimatorStateMachine stateMachine = AC.layers[3].stateMachine;

        foreach (ChildAnimatorState state in stateMachine.states)
            if (state.state.name.Contains("@"))
                stateMachine.RemoveState(state.state);
    }

    void AddRetunTransitions(AnimatorState SRC, AnimatorState DST)
    {
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 3; j++)
            {
                AnimatorStateTransition CurrAST = SRC.AddTransition(DST);
                CurrAST.AddCondition(AnimatorConditionMode.If, 1, $"ES/{i}{j}");
                CurrAST.hasExitTime = false;
                CurrAST.exitTime = 0;
                CurrAST.duration = 0.02f;
            }
    }

    public static Vector3 CalculateGridPosition(int page, int row, int col, int numColumns, int spacingX, int spacingY, int startingPosX, int startingPosY)
    {
        int totalRows = (row + 1) * 2;

        float x = (col + page * numColumns) * spacingX;
        float y = -(totalRows - 1) * spacingY;

        x += startingPosX;
        y += startingPosY;

        return new Vector3(x, y);
    }

    public static AnimatorState GetStateByName(string stateName, AnimatorStateMachine stateMachine)
    {
        foreach (ChildAnimatorState state in stateMachine.states)
            if (state.state.name == stateName)
                return state.state;

        return null; // State not found
    }

    Material CreateMatFromWithImage(Material Mat, Texture2D IMG)
    {
        Material NewMat = new(Mat);
        NewMat.SetTexture("_DecalTexture1", IMG);

        return NewMat;
    }

    GameObject[] GetAllChildrensOf(GameObject gameObject)
    {
        GameObject[] AllGMIn = new GameObject[gameObject.transform.childCount];
        for (int i = 0; i < AllGMIn.Length; i++)
            if (gameObject.transform.childCount > 0)
            {
                AllGMIn[i] = gameObject.transform.GetChild(i).gameObject;
            }
        return AllGMIn;
    }

    Texture2D LoadTextureFromFile(string filePath)
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>(filePath);
    }

    Texture2D Resize(Texture2D texture2D, int targetX, int targetY)
    {
        RenderTexture rt = new(targetX, targetY, 24);
        RenderTexture.active = rt;
        Graphics.Blit(texture2D, rt);

        Texture2D result = new(targetX, targetY);
        result.ReadPixels(new Rect(0, 0, targetX, targetY), 0, 0);
        result.Apply();

        return result;
    }

    void DeferedDestroy()
    {
        EditorApplication.delayCall += () =>
        {
            for (int i = 0; i < DestroyList.Count; i++)
                DestroyImmediate(DestroyList[i]);
        };
    }

    public string FindHierarchyPathFromParent(GameObject parentObject, GameObject targetObject)
    {
        return AnimationUtility.CalculateTransformPath(targetObject.transform, parentObject.transform.parent);
    }

}

#endif