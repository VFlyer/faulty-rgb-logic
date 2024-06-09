using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MalfunctioningRGBLogicScript : MonoBehaviour {

	public KMNeedyModule needyHandler;
	public KMAudio mAudio;
	public KMColorblindMode colorblindMode;
	public KMBombInfo bombInfo;
	public MeshRenderer[] leftGridRends, rightGridRends, centerGridRends;
	public TextMesh centerText, timerOverrideMesh;
	public TextMesh[] cbLeftText, cbRightText;
	public KMSelectable[] gridSelectable;

	static int modIDCnt;
	int moduleID;
	bool[] expectedCells, pressedCells, glitchedLeft, glitchedRight;
	int[] colorIdxLeftGrid, colorIdxRightGrid;
	bool needyActive = false, needySolved = false, glitchNeedyTimer, expectedInverted, alreadyStruck = false, colorblind;
	Transform storedNeedyTimerRef;
	IEnumerator needyTimeAlterer;
	List<IEnumerator> squareFlashers;
	const int amountSquares = 16;
	int activationCount = 0;
	readonly static char[] operSymbols = new[] { '\x2227', '\x2228', '\x22bb', '\x22bc', '\x22bd', '\x2194' };
    const string colorNameAbbrev = "KBGCRMYW-", channelAbbrev = "BGR";
	public Color[] colorsRefs;
	const float chanceUndeductable = 0.2f,
		chanceFlipVars = 0.2f,
		chanceInvertExpected = 0.2f,
		chanceDeductableAlternate = 0.3f;

	void QuickLog(string toLog, params object[] args)
    {
		Debug.LogFormat("[{0} #{1}] {2}", needyHandler.ModuleDisplayName, moduleID, string.Format(toLog, args));
	}

	// Use this for initialization
	void Start () {
		moduleID = ++modIDCnt;
		needyHandler.OnNeedyActivation += HandleNeedyActivation;
		needyHandler.OnTimerExpired += HandleNeedyExpire;
		needyHandler.OnNeedyDeactivation += HandleNeedyForceDeactivate;
		StartCoroutine(DelayAlterNeedyTimerBase());
        for (var x = 0; x < gridSelectable.Length; x++)
        {
			var y = x;
			gridSelectable[x].OnInteract += delegate {
				CellPress(y);
				return false;
			};
        }
	}
	void CellPress(int idx)
    {
		if (needySolved || !needyActive) return;
		if (!pressedCells[idx])
        {
			pressedCells[idx] = true;
			gridSelectable[idx].AddInteractionPunch(0.5f);
			if (!expectedCells[idx])
			{
				mAudio.PlaySoundAtTransform("BlipSelectBad", transform);
				QuickLog("{0}{1} was not safe.", "ABCD"[idx % 4].ToString(), idx / 4 + 1);
				if (!alreadyStruck)
					needyHandler.HandleStrike();
				else
					mAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.Strike, transform);
				alreadyStruck = true;
			}
			else
				mAudio.PlaySoundAtTransform("BlipSelect", transform);
			centerGridRends[idx].material.color = !expectedInverted ^ expectedCells[idx] ? Color.red : Color.white;
        }
    }

	void HandleNeedyActivation()
	{
		if (needySolved)
		{
			needyHandler.HandlePass();
			return;
		}
		var attemptCnt = 1;
	retryExpected:
		expectedCells = new bool[amountSquares];
		pressedCells = new bool[amountSquares];
		colorIdxLeftGrid = new int[amountSquares];
		colorIdxRightGrid = new int[amountSquares];
		squareFlashers = new List<IEnumerator>();
		var leftGridDeductableCells = Enumerable.Repeat(true, amountSquares).ToArray();
		var rightGridDeductableCells = Enumerable.Repeat(true, amountSquares).ToArray();
		for (var x = 0; x < amountSquares; x++)
		{
			leftGridDeductableCells[x] ^= Random.value < chanceUndeductable;
			colorIdxLeftGrid[x] = leftGridDeductableCells[x] ? Random.Range(0, 8) : 8;
			rightGridDeductableCells[x] ^= Random.value < chanceUndeductable;
			colorIdxRightGrid[x] = rightGridDeductableCells[x] ? Random.Range(0, 8) : 8;
		}
		var chnUseLeft = Random.Range(0, 3);
		var chnUseRight = Random.Range(0, 3);
		var idxOperatorUsed = Random.Range(0, operSymbols.Length);
		var flipVariables = Random.value < chanceFlipVars;
		var invertLeft = Random.value < 0.5f;
		var invertRight = Random.value < 0.5f;
		var invertOutput = Random.value < 0.5f;
		expectedInverted = Random.value < chanceInvertExpected;
		var deductedTiles = new bool[amountSquares];
		for (var x = 0; x < amountSquares; x++)
		{
			var stateLeft = (colorIdxLeftGrid[x] >> chnUseLeft) % 2 == 1;
			var stateRight = (colorIdxRightGrid[x] >> chnUseRight) % 2 == 1;

			if (leftGridDeductableCells[x] && rightGridDeductableCells[x])
			{ // If both cells can be deducted, just grab the result of this.
				deductedTiles[x] = true;
				expectedCells[x] = (flipVariables ? ApplyOperation(stateRight ^ invertLeft, stateLeft ^ invertRight, idxOperatorUsed) :
					ApplyOperation(stateLeft ^ invertLeft, stateRight ^ invertRight, idxOperatorUsed)) ^ invertOutput;
			}
			else if (leftGridDeductableCells[x])
			{
				var resultsDistinct = new[] { true, false }.Select(a => (flipVariables ? ApplyOperation(a ^ invertLeft, stateLeft ^ invertRight, idxOperatorUsed) :
					ApplyOperation(stateLeft ^ invertLeft, a ^ invertRight, idxOperatorUsed)) ^ invertOutput).Distinct();
				if (resultsDistinct.Count() == 1)
				{
					deductedTiles[x] = true;
					expectedCells[x] = resultsDistinct.Single();
				}
			}
			else if (rightGridDeductableCells[x])
			{
				var resultsDistinct = new[] { true, false }.Select(a => (flipVariables ? ApplyOperation(stateRight ^ invertLeft, a ^ invertRight, idxOperatorUsed) :
					ApplyOperation(a ^ invertLeft, stateRight ^ invertRight, idxOperatorUsed)) ^ invertOutput).Distinct();
				if (resultsDistinct.Count() == 1)
				{
					deductedTiles[x] = true;
					expectedCells[x] = resultsDistinct.Single();
				}
			}
		}
		if (expectedInverted)
			for (var x = 0; x < amountSquares; x++)
				expectedCells[x] ^= deductedTiles[x];
		if (!expectedCells.Any(a => a) && attemptCnt < 5)
		{
			attemptCnt++;
			goto retryExpected;
		}
		var initialExpression = invertOutput
			? string.Format("!({0}{1}{2}{3}{4})",
				invertLeft ? "!" : "",
				(flipVariables ? channelAbbrev[chnUseRight] : channelAbbrev.ToLower()[chnUseLeft]).ToString(),
				operSymbols[idxOperatorUsed],
				invertRight ? "!" : "",
				(flipVariables ? channelAbbrev.ToLower()[chnUseLeft] : channelAbbrev[chnUseRight]).ToString())
			: string.Format("{0}{1}{2}{3}{4}",
				invertLeft ? "!" : "",
				(flipVariables ? channelAbbrev[chnUseRight] : channelAbbrev.ToLower()[chnUseLeft]).ToString(),
				operSymbols[idxOperatorUsed],
				invertRight ? "!" : "",
				(flipVariables ? channelAbbrev.ToLower()[chnUseLeft] : channelAbbrev[chnUseRight]).ToString());
		centerText.color = expectedInverted ? Color.red : Color.white;
		var shiftExpression = Random.value < 0.2f ? 0 : Random.Range(1, initialExpression.Length);
		centerText.text = initialExpression.Skip(shiftExpression).Concat(initialExpression.Take(shiftExpression)).Join("");
		glitchedLeft = new bool[amountSquares];
		glitchedRight = new bool[amountSquares];
		for (var x = 0; x < amountSquares; x++)
		{
			var y = x;
			if (!leftGridDeductableCells[x])
			{
				glitchedLeft[x] = true;
				squareFlashers.Add(FlashSquareWhileActive(leftGridRends[y], colorsRefs.ToArray().Shuffle(), initialDelay: Random.value * 3));
			}
			else if (colorIdxLeftGrid[x] != 0 && Random.value < chanceDeductableAlternate)
			{
				var allowedCombos = new List<int[]>();
				for (var p = 0; p < 8; p++)
					for (var n = p + 1; n < 8; n++)
						if ((n ^ p) == colorIdxLeftGrid[x])
							allowedCombos.Add(new[] { n, p });
				glitchedLeft[x] = true;
				squareFlashers.Add(FlashSquareWhileActive(leftGridRends[y], allowedCombos.PickRandom().Select(a => colorsRefs[a]).ToArray().Shuffle(), initialDelay: Random.value * 3, flashDelay: 0.5f, deducted: true, cbTextRelevant: cbLeftText[y]));
			}
			else
				leftGridRends[x].material.color = colorsRefs[colorIdxLeftGrid[x]];
			if (!rightGridDeductableCells[x])
			{
				glitchedRight[x] = true;
				squareFlashers.Add(FlashSquareWhileActive(rightGridRends[y], colorsRefs.ToArray().Shuffle(), initialDelay: Random.value * 3));
			}
			else if (colorIdxRightGrid[x] != 0 && Random.value < chanceDeductableAlternate)
			{
				glitchedRight[x] = true;
				var allowedCombos = new List<int[]>();
				for (var p = 0; p < 8; p++)
					for (var n = p + 1; n < 8; n++)
						if ((n ^ p) == colorIdxRightGrid[x])
							allowedCombos.Add(new[] { n, p });
				squareFlashers.Add(FlashSquareWhileActive(rightGridRends[y], allowedCombos.PickRandom().Select(a => colorsRefs[a]).ToArray().Shuffle(), initialDelay: Random.value * 3, flashDelay: 0.5f, deducted: true, cbTextRelevant: cbRightText[y]));
			}
			else
				rightGridRends[x].material.color = colorsRefs[colorIdxRightGrid[x]];
		}
		QuickLog("Activation #{0}:", ++activationCount);
		QuickLog("The left grid's true colours are");
		for (var x = 0; x < 4; x++)
			QuickLog(colorIdxLeftGrid.Skip(4 * x).Take(4).Select(a => colorNameAbbrev[a]).Join());
		QuickLog("The right grid's true colours are");
		for (var x = 0; x < 4; x++)
			QuickLog(colorIdxRightGrid.Skip(4 * x).Take(4).Select(a => colorNameAbbrev[a]).Join());
		QuickLog("The expression displayed is {0} shifted to the left by {1}", initialExpression, shiftExpression);
		QuickLog("The following positions are deductable:");
		for (var x = 0; x < 4; x++)
			QuickLog(deductedTiles.Skip(4 * x).Take(4).Select(a => a ? "!" : "X").Join());
		QuickLog("{0}The following tiles should be pressed:", expectedInverted ? "Condition inverted. " : "");
		for (var x = 0; x < 4; x++)
			QuickLog(expectedCells.Skip(4 * x).Take(4).Select(a => a ? "O" : "X").Join());
		needyActive = true;
		alreadyStruck = false;
		for (var x = 0; x < amountSquares; x++)
        {
			cbLeftText[x].text = !colorblind || glitchedLeft[x] ? "" : colorNameAbbrev[colorIdxLeftGrid[x]].ToString();
			cbRightText[x].text = !colorblind || glitchedRight[x] ? "" : colorNameAbbrev[colorIdxRightGrid[x]].ToString();
        }
		foreach (var handler in squareFlashers)
			StartCoroutine(handler);
        StartCoroutine(HandleTimedActivation());
	}
	bool ApplyOperation(bool a, bool b, int idxOperator)
    {
		switch (idxOperator)
        {
			case 0: // AND
				return a && b;
			case 1: // OR
				return a || b;
			case 2: // XOR
				return a ^ b;
			case 3: // NAND
				return !(a && b);
			case 4: // NOR
				return !(a || b);
			case 5: // XNOR
				return a ^ !b;
        }
		return a;
    }

	void HandleNeedyForceDeactivate()
    {
		needyActive = false;
		needySolved = true;
		StopAllCoroutines();
		HideAllContent();
		timerOverrideMesh.text = "GG";
	}
	void HideAllContent()
    {
		if (needyTimeAlterer != null)
			StopCoroutine(needyTimeAlterer);
		glitchNeedyTimer = false;
		centerText.text = "";
		centerText.color = Color.white;
		timerOverrideMesh.text = "";
		for (var x = 0; x < leftGridRends.Length; x++)
			leftGridRends[x].material.color = Color.black;
		for (var x = 0; x < centerGridRends.Length; x++)
			centerGridRends[x].material.color = Color.black;
		for (var x = 0; x < rightGridRends.Length; x++)
			rightGridRends[x].material.color = Color.black;
	}

	void HandleNeedyExpire()
    {
		needyActive = false;
		HideAllContent();
		var requireStrike = false;
		var missedCellIdxes = new List<int>();
		for (var x = 0; x < amountSquares; x++)
		{
			if (expectedCells[x] && !pressedCells[x])
			{
				requireStrike = true;
				missedCellIdxes.Add(x);
			}
		}
		if (requireStrike)
		{
			QuickLog("Following cells were missed upon deactivation: {0}", missedCellIdxes.Select(a => "ABCD"[a % 4].ToString() + (a / 4 + 1).ToString()).Join(", "));
			if (!alreadyStruck)
				needyHandler.HandleStrike();
			else
				mAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.Strike, transform);
			StartCoroutine(FlashCenterMissed(missedCellIdxes));
		}
	}
	IEnumerator FlashCenterMissed(IEnumerable<int> idxMissed)
    {
		for (float t = 0; t < 1f; t += Time.deltaTime)
        {
			foreach (var idx in idxMissed)
				centerGridRends[idx].material.color = Color.Lerp(Color.black, expectedInverted ? Color.red : Color.white, 1f - Mathf.Abs(2 * (t - 0.5f)));
			yield return null;
        }
		foreach (var idx in idxMissed)
			centerGridRends[idx].material.color = Color.black;
	}
	IEnumerator FlashSquareWhileActive(MeshRenderer affectedRenderer, IEnumerable<Color> possibleColors, float flashDelay = 0.33f, float initialDelay = 0f, bool deducted = false, TextMesh cbTextRelevant = null)
    {
		yield return new WaitForSeconds(initialDelay);
		var idxCur = Random.Range(0, possibleColors.Count());
		while (needyActive)
        {
			affectedRenderer.material.color = possibleColors.ElementAt(idxCur);
			if (deducted && cbTextRelevant != null)
				cbTextRelevant.text = colorblind ? colorNameAbbrev[colorsRefs.ToList().IndexOf(possibleColors.ElementAt(idxCur))].ToString() : "";
			yield return new WaitForSeconds(flashDelay);
			idxCur = (idxCur + 1) % possibleColors.Count();
        }
    }
	IEnumerator HandleTimedActivation()
    {
		needyTimeAlterer = GlitchTimerText();
		var selectedTimeLeft = Random.Range(10, 100);
		while (needyHandler.GetNeedyTimeRemaining() > selectedTimeLeft)
			yield return null;
		StartCoroutine(needyTimeAlterer);
	}

	IEnumerator DelayAlterNeedyTimerBase()
	{
		var needyTimer = gameObject.transform.Find("NeedyTimer(Clone)");
		if (needyTimer != null)
		{
			storedNeedyTimerRef = needyTimer;
			//needyTimer.gameObject.SetActive(false);	
		}
		yield break;
	}
	IEnumerator GlitchTimerText()
    {
		glitchNeedyTimer = true;
		while (needyActive)
        {
			yield return new WaitForSeconds(0.1f);
			var randomValue = Random.Range(0, 100);
			timerOverrideMesh.text = randomValue.ToString("00");
		}
		glitchNeedyTimer = false;
    }

	// Update is called once per frame
	void Update () {
		if (!(glitchNeedyTimer || needySolved))
			timerOverrideMesh.text = needyActive ? needyHandler.GetNeedyTimeRemaining().ToString("00") : needySolved ? "GG" : "";
		if (needyActive)
		{
			var percentLeft = needyHandler.GetNeedyTimeRemaining() / needyHandler.CountdownTime;
			timerOverrideMesh.color = percentLeft <= 0.5f ? Color.Lerp(Color.red, Color.yellow, 2 * percentLeft) : Color.Lerp(Color.yellow, Color.green, 2 * (percentLeft - .5f));
		}
		else
			timerOverrideMesh.color = Color.red;
		if (storedNeedyTimerRef != null)
        {
			var needyTimerText = storedNeedyTimerRef.Find("SevenSegText");
			if (needyTimerText != null)
				needyTimerText.gameObject.SetActive(false);
		}
	}
	void HandleColorblindToggle()
    {
		if (needyActive)
		{
			for (var x = 0; x < cbLeftText.Length; x++)
				if (!glitchedLeft[x])
					cbLeftText[x].text = colorblind ? colorNameAbbrev[colorIdxLeftGrid[x]].ToString() : "";
			for (var x = 0; x < cbRightText.Length; x++)
				if (!glitchedRight[x])
					cbRightText[x].text = colorblind ? colorNameAbbrev[colorIdxRightGrid[x]].ToString() : "";
		}
	}


#pragma warning disable 414
	private readonly string TwitchHelpMessage = @"!{0} <a-d><1-4> [Presses cell] | !{0} cb [Colourblind Mode] | Cell presses can be chained, separated with spaces i.e. !{0} a1 b2 c3 d4";
#pragma warning restore 414

	private IEnumerator ProcessTwitchCommand(string command)
	{
		if (command.ToLowerInvariant() == "cb")
		{
			yield return null;
			colorblind ^= true;
			HandleColorblindToggle();
			yield break;
		}
		if (needyActive)
		{
			string[] cells = command.ToLowerInvariant().Split(' ');
			int[] indices = new int[cells.Length];
			for (int i = 0; i < cells.Length; i++)
			{
				if ("abcd".Contains(cells[i][0]) && "1234".Contains(cells[i][1]))
				{
					indices[i] = "abcd".IndexOf(cells[i][0]) + ("1234".IndexOf(cells[i][1]) * 4);
				}
				else
				{
					yield return "sendtochaterror \"" + cells[i] + "\" is not a valid cell";
					yield break;
				}
			}
			for (int i = 0; i < indices.Length; i++)
			{
				yield return null;
				gridSelectable[indices[i]].OnInteract();
				if (expectedCells[indices[i]])
					yield return new WaitForSeconds(0.1f);
				else
					yield break;
			}
		}
		else
		{
			yield return "sendtochaterror Module is inactive. Cells cannot be pressed.";
			yield break;
		}
	}
}
