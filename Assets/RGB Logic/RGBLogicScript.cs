using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public class RGBLogicScript : MonoBehaviour {

    public KMAudio Audio;
    public KMNeedyModule module;
    public KMColorblindMode cbmode;
    public KMSelectable[] buttons;
    public Renderer[] gridL;
    public Renderer[] gridM;
    public Renderer[] gridR;
    public Material[] ledcols;
    public TextMesh display;
    public TextMesh[] cblabels;

    private bool cb;
    private bool active;
    private bool[][][] grids = new bool[2][][] { new bool[3][] { new bool[16], new bool[16], new bool[16] }, new bool[3][] { new bool[16], new bool[16], new bool[16] } };
    private int[][] cellcols = new int[2][] { new int[16], new int[16] };
    private bool[] truthgrid = new bool[16];
    private bool[] inputgrid = new bool[16];
    private bool[] pressed = new bool[16];
    private string[] statement = new string[3];

    private static int moduleIDCounter = 1;
    private int moduleID;

    private void Awake()
    {
        moduleID = moduleIDCounter++;
        cb = cbmode.ColorblindModeActive;
        module.OnNeedyActivation = Activate;
        module.OnTimerExpired = Deactivate;
        for (int i = 0; i < buttons.Length; i++)
        {
            KMSelectable button = buttons[i];
            int b = i;
            button.OnInteract += delegate () { if (active && !pressed[b]) Press(b); return false; };
        }
    }

    private void Activate()
    {
        active = true;
        int[] logic = new int[3];
        grids = new bool[2][][] { new bool[3][] { new bool[16], new bool[16], new bool[16] }, new bool[3][] { new bool[16], new bool[16], new bool[16] } };
        string[][][] celllog = new string[2][][] { new string[4][] { new string[4], new string[4], new string[4], new string[4] }, new string[4][] { new string[4], new string[4], new string[4], new string[4] } };
        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                for (int k = 0; k < 8; k++)
                {
                    int r = Random.Range(0, 16);
                    while (grids[i][j][r])
                        r = Random.Range(0, 16);
                    grids[i][j][r] = true;
                    cellcols[i][r] += (int)Mathf.Pow(2, 2 - j);
                }
            }
            if (i == 0)
                for (int j = 0; j < 16; j++)
                    gridL[j].material = ledcols[cellcols[0][j]];
            else
                for (int j = 0; j < 16; j++)
                    gridR[j].material = ledcols[cellcols[1][j]];
            for (int j = 0; j < 4; j++)
                for (int k = 0; k < 4; k++)
                {
                    celllog[i][j][k] = "KBGCRMYW"[cellcols[i][(4 * j) + k]].ToString();
                    if (cb)
                        cblabels[(16 * i) + (4 * j) + k].text = celllog[i][j][k];
                }
        }
        Debug.LogFormat("[RGB Logic #{0}] The left grid has the colours:\n[RGB Logic #{0}] {1}", moduleID, string.Join("\n[RGB Logic #" + moduleID + "] ", celllog[0].Select(i => string.Join(" ", i)).ToArray()));
        Debug.LogFormat("[RGB Logic #{0}] The right grid has the colours:\n[RGB Logic #{0}] {1}", moduleID, string.Join("\n[RGB Logic #" + moduleID + "] ", celllog[1].Select(i => string.Join(" ", i)).ToArray()));
        bool[][] getgrid = new bool[2][] { new bool[16], new bool[16] };
        for (int i = 0; i < 2; i++)
        {
            logic[i] = Random.Range(0, 6);
            if (logic[i] > 2)
            {
                statement[i * 2] = "!";
                for (int j = 0; j < 16; j++)
                    getgrid[i][j] = !grids[i][logic[i] % 3][j];
            }
            else
                for (int j = 0; j < 16; j++)
                    getgrid[i][j] = grids[i][logic[i] % 3][j];
            statement[i * 2] += "RGB"[logic[i] % 3].ToString();
        }
        switch (Random.Range(0, 6))
        {
            case 0:
                statement[1] = " \x2227 ";
                for (int i = 0; i < 16; i++)
                    truthgrid[i] = getgrid[0][i] && getgrid[1][i];
                break;
            case 1:
                statement[1] = " \x2228 ";
                for (int i = 0; i < 16; i++)
                    truthgrid[i] = getgrid[0][i] || getgrid[1][i];
                break;
            case 2:
                statement[1] = " \x22bb ";
                for (int i = 0; i < 16; i++)
                    truthgrid[i] = getgrid[0][i] ^ getgrid[1][i];
                break;
            case 3:
                statement[1] = " \x22bc ";
                for (int i = 0; i < 16; i++)
                    truthgrid[i] = !(getgrid[0][i] && getgrid[1][i]);
                break;
            case 4:
                statement[1] = " \x22bd ";
                for (int i = 0; i < 16; i++)
                    truthgrid[i] = !(getgrid[0][i] || getgrid[1][i]);
                break;
            case 5:
                statement[1] = " \x2194 ";
                for (int i = 0; i < 16; i++)
                    truthgrid[i] = !(getgrid[0][i] ^ getgrid[1][i]);
                break;
        }
        display.text = string.Join("", statement);
        Debug.LogFormat("[RGB Logic #{0}] The condition is: {1}", moduleID, string.Join(" ", statement));
        Debug.LogFormat("[RGB Logic #{0}] The left condition is met by: {1}", moduleID, string.Join(" ", getgrid[0].Select((x, i) => "ABCD"[i % 4].ToString() + ((i / 4) + 1).ToString()).Where((x, i) => getgrid[0][i]).OrderBy(x => x).ToArray()));
        Debug.LogFormat("[RGB Logic #{0}] The right condition is met by: {1}", moduleID, string.Join(" ", getgrid[1].Select((x, i) => "ABCD"[i % 4].ToString() + ((i / 4) + 1).ToString()).Where((x, i) => getgrid[1][i]).OrderBy(x => x).ToArray()));
        Debug.LogFormat("[RGB Logic #{0}] The full condition is met by: {1}", moduleID, string.Join(" ", truthgrid.Select((x, i) => "ABCD"[i % 4].ToString() + ((i / 4) + 1).ToString()).Where((x, i) => truthgrid[i]).OrderBy(x => x).ToArray()));
    }

    private void Deactivate()
    {
        if (truthgrid.Where((x, i) => x ^ inputgrid[i]).Any())
        {
            module.HandleStrike();
            Debug.LogFormat("[RGB Logic #{0}] True cell(s) missed: {1}", moduleID, string.Join(" ", truthgrid.Select((x, i) => "ABCD"[i % 4].ToString() + ((i / 4) + 1).ToString()).Where((x, i) => truthgrid[i] ^ inputgrid[i]).OrderBy(x => x).ToArray()));
        }
        else
        {
            module.HandlePass();
        }
        active = false;
        display.text = string.Empty;
        foreach (Renderer cell in gridL)
            cell.material = ledcols[0];
        foreach (Renderer cell in gridM)
            cell.material = ledcols[0];
        foreach (Renderer cell in gridR)
            cell.material = ledcols[0];
        for (int i = 0; i < 16; i++)
        {
            for (int j = 0; j < 2; j++)
                cellcols[j][i] = 0;
            truthgrid[i] = false;
            inputgrid[i] = false;
            pressed[i] = false;
        }
        for (int i = 0; i < 3; i++)
            statement[i] = string.Empty;
    }

    private void Press(int b)
    {
        pressed[b] = true;
        buttons[b].AddInteractionPunch(0.5f);
        if (truthgrid[b])
        {
            inputgrid[b] = true;
            gridM[b].material = ledcols[7];
            Audio.PlaySoundAtTransform("BlipSelect", transform);
        }
        else
        {
            module.HandleStrike();
            gridM[b].material = ledcols[4];
            Audio.PlaySoundAtTransform("BlipSelectBad", transform);
            Debug.LogFormat("[RGB Logic #{0}] False cell ({1}{2}) selected", moduleID, "ABCD"[b % 4], (b % 4) + 1);
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
            cb = true;
            if (active)
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 16; j++)
                    cblabels[(16 * i) + j].text = "KBGCRMYW"[cellcols[i][j]].ToString();
            yield break;
        }
        if (active)
        {
            string[] cells = command.ToLowerInvariant().Split(' ');
            int[] indices = new int[cells.Length];
            for(int i = 0; i < cells.Length; i++)
            {
                if("abcd".Contains(cells[i][0]) && "1234".Contains(cells[i][1]))
                {
                    indices[i] = "abcd".IndexOf(cells[i][0]) + ("1234".IndexOf(cells[i][1]) * 4);
                }
                else
                {
                    yield return "sendtochaterror \"" + cells[i] + "\" is not a valid cell";
                    yield break;
                }
            }
            for(int i = 0; i < indices.Length; i++)
            {
                yield return null;
                buttons[indices[i]].OnInteract();
            }
        }
        else
        {
            yield return "sendtochaterror Module is inactive. Cells cannot be pressed.";
            yield break;
        }
    }
}
