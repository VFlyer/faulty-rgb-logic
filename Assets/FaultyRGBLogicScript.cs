using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FaultyRGBLogicScript : MonoBehaviour {

	public KMNeedyModule needyHandler;
	public KMAudio mAudio;
	public KMColorblindMode colorblindMode;
	public KMBombInfo bombInfo;
	public MeshRenderer[] leftGrid, rightGrid, centerGrid;
	public TextMesh centerText, timerMesh;

	static int modIDCnt;
	int moduleID;
	bool[] expectedCells, pressedCells;
	bool needyActive = false;
	Transform storedNeedyTimerRef;

	void QuickLog(string toLog, params object[] args)
    {
		Debug.LogFormat("[{0} #{1}] {2}", needyHandler.ModuleDisplayName, moduleID, string.Format(toLog, args));
	}

	// Use this for initialization
	void Start () {
		moduleID = ++modIDCnt;
		needyHandler.OnNeedyActivation += HandleNeedyActivation;
		needyHandler.OnTimerExpired += delegate { };
		StartCoroutine(DelayRotation());
	}
	void HandleNeedyActivation()
    {
		needyActive = true;
    }
	void HandleNeedyExpire()
    {
		needyActive = false;
    }


	IEnumerator DelayRotation()
	{
		var needyTimer = gameObject.transform.Find("NeedyTimer(Clone)");
		if (needyTimer != null)
		{
			storedNeedyTimerRef = needyTimer;
			//needyTimer.gameObject.SetActive(false);	
		}
		yield break;
	}

	// Update is called once per frame
	void Update () {
		timerMesh.text = needyActive ? needyHandler.GetNeedyTimeRemaining().ToString("00") : "";
		if (storedNeedyTimerRef != null)
        {
			var needyTimerText = storedNeedyTimerRef.Find("SevenSegText");
			if (needyTimerText != null)
				needyTimerText.gameObject.SetActive(false);
		}
	}
}
