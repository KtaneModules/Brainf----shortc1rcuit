using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using System.Text.RegularExpressions;

public class TheSimpletonScript : MonoBehaviour
{
	public KMBombModule Module;
	public KMAudio Audio;
	public KMBombInfo Info;
	
	public TextMesh Victory;
	public AudioClip SFX;
	
	public KMSelectable Button;
	private int Once = 0;

	//Logging
    static int moduleIdCounter = 1;
    int moduleId;
    private bool ModuleSolved;
	
	void Awake()
	{
		moduleId = moduleIdCounter++;
		Button.OnInteract += delegate () {Pressed(); return false; };
		Button.OnInteractEnded += delegate () {Unpressed();};
	}

    void Start()
    {
        Debug.LogFormat("[The Simpleton #{0}] Push the button!", moduleId);
    }
	
	void Pressed()
	{
		if (Once != 2)
		{
			Once = 1;
		}
		Button.AddInteractionPunch();
		Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonPress, transform);
	}
	
	void Unpressed()
	{
		Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonRelease, transform);
		if (Once == 1)
		{
			Debug.LogFormat("[The Simpleton #{0}] You pushed the button. Good job!", moduleId);
			//Audio.PlaySoundAtTransform(SFX.name, transform);
			Victory.text = "VICTORY";
			Module.HandlePass();
			Once = 2;
		}
	}

    //twitch plays
    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} push [Pushes the button]";
    #pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        if (Regex.IsMatch(command, @"^\s*push\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            Button.OnInteract();
            yield return new WaitForSeconds(0.1f);
            Button.OnInteractEnded();
            yield break;
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        Button.OnInteract();
        yield return new WaitForSeconds(0.1f);
        Button.OnInteractEnded();
    }
}