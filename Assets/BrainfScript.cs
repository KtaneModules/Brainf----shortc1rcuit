/*MESSAGE TO ANY FUTURE CODERS:
 PLEASE COMMENT YOUR WORK
 I can't stress how important this is especially with bomb types such as boss modules.
 If you don't it makes it realy hard for somone like me to find out how a module is working so I can learn how to make my own.
 Please comment your work.
 Short_c1rcuit*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using UnityEngine;
using KModkit;

public class BrainfScript : MonoBehaviour
{
    //Gets audio clips and info about the bomb.
    public KMAudio audio;
    public KMBombInfo bomb;

    //Sets up the text meshes so they can be changed later
    public TextMesh symbolMesh;
    public TextMesh stageMesh;

    //Defines the keys on the keypad
    public KMSelectable[] keypad;

    //array to store the randomly generated program
    char[] script;

    //List to store all the answers
    List<string> answers = new List<string>();

    //Array to store ignored modules
    string[] Ignoreds;

    //A ticker is used to not overwhelm the computer running the code by running the check code every 5 ticks
    int ticker;

    //int to store the amount of solved modules
    int solvedModules = -1;

    //Logging
    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    //Asks whether the full stop on the screen has been given an answer
    bool fullStopSolved = true;

    //Used to blank the stage number for user input
    bool inputStarted;

    //Twitch help message
#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Input the answer with “!{0} (answer)”, eg: “!{0} 28” to input 28. Use “!{0} clr” to clear the input and “!{0} ok” to submit.";
#pragma warning restore 414

    public IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.ToLowerInvariant().Trim();

        if (fullStopSolved)
        {
            yield return "sendtochaterror Now is not the time to input.";
        }

        //If the input is a 1 or 2 digit number
        if (Regex.IsMatch(command, @"^\d{1,2}"))
        {
            //Goes through each digit to 
            foreach (char digit in command)
            {
                yield return null;
                keypad[int.Parse(digit.ToString())].OnInteract();
            }
        }
        else if (command == "ok")
        {
            if (!fullStopSolved)
            {
                yield return null;
                keypad[10].OnInteract();
                if (fullStopSolved & answers.Count != 0)
                {
                    yield return "awardpoints 5";
                }
            }
        }
        else if (command == "clr")
        {
            yield return null;
            keypad[11].OnInteract();
        }
        else
        {
            yield return "sendtochaterror The command you inputted is incorrect.";
        }
    }

    void TwitchHandleForcedSolve()
    {
        GetComponent<KMBombModule>().HandlePass();
        moduleSolved = true;
    }

    void Awake()
    {
        symbolMesh.text = "-";
        stageMesh.text = "00";

        //More logging stuff
        moduleId = moduleIdCounter++;

        //Takes the keys and gives them their methods
        foreach (KMSelectable key in keypad)
        {
            KMSelectable pressedKey = key;
            key.OnInteract += delegate () { KeyPress(pressedKey); return false; };
        }
    }

    //Used to randomly generate a program in Brainf--- for the module to use
    char[] GenerateProgram(int size)
    {
        //amount of characters inside the loop + 1
        int loopsize;
        //How many characters go before the loop
        int beforeloop;

        //array to store the randomly generated program in the method. We add one to store the fullstop at the end
        char[] program = new char[size + 1];

        int i = 0;

        //Pass 1: adding loops
        while (i < size)
        {
            //sets the gap before the loop (1-5)
            beforeloop = UnityEngine.Random.Range(1, 6);
            //sets the amount of characters in the loop (1-5)
            loopsize = UnityEngine.Random.Range(2, 7);

            //If you cant fit in the loop
            if (beforeloop + loopsize + i >= size)
            {
                //exit Pass 1
                break;
            }
            //Sets the start and end of the loop
            program[i + beforeloop] = '[';
            program[i + beforeloop + loopsize] = ']';

            //Moves froward
            i += beforeloop + loopsize + 1;
        }

        //bool to see whether there is a loop that isn't finished
        bool openloop = false;

        //Used to space out commas and fullstops (commapos is set to -5 to allow it to appear from the start)
        int commapos = -5;
        int fullstoppos = 0;

        //Pass 2: Add other characters
        for (int j = 0; j < size; j++)
        {
            //array to store all the types of Brainf--- characters (except loops)
            List<char> symbols = new List<char> { '<', '>', '+', '-', ',', '.' };

            //Makes note of whether the loop is open or not
            if (program[j] == '[')
            {
                openloop = true;
                continue;
            }
            else if(program[j] == ']')
            {
                openloop = false;
                continue;
            }

            //Doesn't allow , and . to appear in a loop
            if (openloop)
            {
                symbols.Remove(',');
                symbols.Remove('.');
            }

            //Doesn't allow commas/fullstops to appear within 5 steps of annother
            if (j - commapos < 5 || size - j < 5)
            {
                symbols.Remove(',');
            }
            if (j - fullstoppos < 5 || size - j < 5)
            {
                symbols.Remove('.');
            }

            //Gives a valid symbol to the program
            program[j] = symbols[UnityEngine.Random.Range(0, symbols.Count)];

            if(program[j] == ',')
            {
                //Updates the position of the last comma
                commapos = j;
            }
            else if (program[j] == '.')
            {
                //Updates the position of the last full stop
                fullstoppos = j;
            }
        }

        //Adds a fullstop to the end to get the defuser to enter the final result
        program[size] = '.';
        return program;
    }

    void AnswerProgram(char[] program)
    {
        //Current position in the tape when running it
        int tapepos = 0;

        //The tape
        List<int> tape = new List<int> { 0, 0 };

        //Int that stores where the loop starts
        int loopstart = 0;

        //Int that stores how many time the loop has looped
        int looptimes = 0;

        //How many steps of solving the module has taken
        int stepcount = 1;

        //Runs through all the symbols in the program and follows them
        for (int i = 0; i < program.Count(); i++)
        {
            Debug.LogFormat("[Brainf--- #{0}] Step {1}:", moduleId, stepcount);

            if (program[i] == '<')
            {
                //If you are about to go to a negative position, go right instead of left.
                if (tapepos == 0)
                {
                    tapepos++;
                    Debug.LogFormat("[Brainf--- #{0}] Going right instead of left, now in position {1} on {2}.", moduleId, tapepos, tape[tapepos]);
                }
                //Else go left
                else
                {
                    tapepos -= 1;
                    Debug.LogFormat("[Brainf--- #{0}] Going left, now in position {1} on {2}.", moduleId, tapepos, tape[tapepos]);
                }
            }
            else if (program[i] == '>')
            {
                tapepos++;
                //Adds annother digit to the tape it we go too far right
                if (tapepos == tape.Count())
                {
                    tape.Add(0);
                }
                Debug.LogFormat("[Brainf--- #{0}] Going right, now in position {1} on {2}.", moduleId, tapepos, tape[tapepos]);
            }
            else if (program[i] == '+')
            {
                tape[tapepos]++;
                Debug.LogFormat("[Brainf--- #{0}] Adding 1 to position {1}. Position {1} is now {2}.", moduleId, tapepos, tape[tapepos]);
            }
            else if (program[i] == '-')
            {
                //If you are about to create a negative number, add one instead of subtracting.
                if (tape[tapepos] == 0)
                {
                    tape[tapepos]++;
                    Debug.LogFormat("[Brainf--- #{0}] Adding 1 to position {1} instead of subtracting. Position {1} is now {2}.", moduleId, tapepos, tape[tapepos]);
                }
                else
                {
                    tape[tapepos] -= 1;
                    Debug.LogFormat("[Brainf--- #{0}] Subtracting 1 from position {1}. Position {1} is now {2}.", moduleId, tapepos, tape[tapepos]);
                }
            }
            else if (program[i] == '[')
            {
                loopstart = i;
                looptimes = 0;
                Debug.LogFormat("[Brainf--- #{0}] Starting loop.", moduleId);
            }
            else if (program[i] == ']')
            {
                //The origanal rules of looping still apply. However, each loop can repeat no more than x times where x = batteries + 1
                if ((tape[tapepos] == 0) | (looptimes == bomb.GetBatteryCount() + 1))
                {
                    Debug.LogFormat("[Brainf--- #{0}] End of loop.", moduleId);
                }
                else
                {
                    looptimes++;
                    i = loopstart;
                    Debug.LogFormat("[Brainf--- #{0}] Going to the start of the loop. You have now looped {1} time(s).", moduleId, looptimes);
                }
            }
            else if (program[i] == ',')
            {
                int y;
                //Take the value of the cell to the left of you (if you are in position 0 take the one to the right of you).
                if (tapepos == 0)
                {
                    y = tape[1];
                }
                else
                {
                    y = tape[tapepos - 1];
                }
                //Modulo the value by 6 and add 1 to it.
                y = (y % 6);

                //Take the character in the serial number that is in the position indicated by the number you just got.
                char character = bomb.GetSerialNumber().ToCharArray()[y];

                //If it is a letter turn it into it's position in the alphabet (A=1, B=2, e.t.c).
                int number;
                bool success = Int32.TryParse(character.ToString(), out number);

                if (!success)
                {
                    //If it is a letter then convert it to a number
                    y = (int)character - 64;
                }
                else
                {
                    y = number;
                }

                //The value of y is asigned to the current cell
                tape[tapepos] = y;

                Debug.LogFormat("[Brainf--- #{0}] Position {1} is now equal to {2}.", moduleId, tapepos, y);
            }
            //This is for when the fullstop comes up
            else
            {
                //Adds it to the list of answers
                answers.Add((tape[tapepos] % 100).ToString());
                Debug.LogFormat("[Brainf--- #{0}] Answer {1} is equal to {2}.", moduleId, answers.Count, (tape[tapepos] % 100).ToString());
            }
            stepcount++;
        }

        //Logging
        string tapestring = "";

        foreach (int number in tape)
        {
            tapestring = tapestring + number.ToString() + ", ";
        }

        //Cuts the last part off the string
        char[] mychar = { ' ', ',' };
        tapestring = tapestring.TrimEnd(mychar);

        Debug.LogFormat("[Brainf--- #{0}] The tape is {1}", moduleId, tapestring);
    }

    // Use this for initialization
    private void Start()
    {
        //Finds all the modules on the bomb that are bosses or other modules that need to be ignored.
        //Make sure you have the KMBossModule script on your module or this will produce an error
        Ignoreds = GetComponent<KMBossModule>().GetIgnoredModules("Brainf---", new string[]{
            "14",
            "Bamboozling Time Keeper",
            "Brainf---",
            "Forget Enigma",
            "Forget Everything",
            "Forget It Not",
            "Forget Me Later",
            "Forget Me Not",
            "Forget Perspective",
            "Forget Them All",
            "Forget This",
            "Forget Us Not",
            "Organization",
            "Purgatory",
            "Simon Forgets",
            "Simon's Stages",
            "Souvenir",
            "Tallordered Keys",
            "The Swan",
            "The Time Keeper",
            "The Troll",
            "The Very Annoying Button",
            "Timing is Everything",
            "Turn The Key",
            "Ultimate Custom Night",
            "Übermodule"
        });

        //Finds how many solvable modules there are that aren't in the Ignoreds list
        int count = bomb.GetSolvableModuleNames().Where(x => !Ignoreds.Contains(x)).Count();

        //Ends the module if there are no solvable modules that aren't in the ignoreds list
        if (count == 0)
        {
            GetComponent<KMBombModule>().HandlePass();
            Debug.LogFormat("[Brainf--- #{0}] Some error occured where the solveable module count is 0. Automatically solving.", moduleId);
            moduleSolved = true;
        }
        else
        {
            //Runs the generate program method with the array size of the value just worked out
            script = GenerateProgram(count);
            Debug.LogFormat("[Brainf--- #{0}] The program is {1}", moduleId, new string(script));

            //Gives us all the answers that we have to input
            AnswerProgram(script);

            Debug.LogFormat("[Brainf--- #{0}] The answers are {1}", moduleId, string.Join(", ", answers.ToArray()));
        }
    }

    void KeyPress(KMSelectable key)
    {
        if (moduleSolved) return;

        //Makes the bomb move when you press it
        key.AddInteractionPunch();

        //Makes a sound when you press the button.
        GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);

        //As the value of each number on the keypad is equivalent to their position in the array, I can get the button's position and use that to work out it's value.
        int number = Array.IndexOf(keypad, key);

        //If the button pressed was clr
        if (number == 11)
        {
            //The stage number display shows the last 2 digits of the stage number
            if (solvedModules < 10)
            {
                stageMesh.text = "0" + solvedModules.ToString();
            }
            else if (solvedModules > 99)
            {
                stageMesh.text = (solvedModules % 100).ToString();
            }
            else
            {
                stageMesh.text = solvedModules.ToString();
            }
            //Clears the inputed data 
            inputStarted = false;
        }
        //If the OK button was pushed
        else if (number == 10)
        {
            //If no-one had inputted anything or now isn't the time to input, it doesn't do anything
            if (inputStarted & !fullStopSolved)
            {
                if (stageMesh.text == answers[0])
                {
                    answers.RemoveAt(0);
                    fullStopSolved = true;
                    Debug.LogFormat("[Brainf--- #{0}] You submitted {1}. Incorrect.", moduleId, stageMesh.text);

                    //Once all the answers have been inputted
                    if (answers.Count == 0)
                    {
                        //Solves the module
                        stageMesh.text = "GG";
                        moduleSolved = true;
                        GetComponent<KMBombModule>().HandlePass();
                        Debug.LogFormat("[Brainf--- #{0}] Module solved.", moduleId);
                    }
                }
                else
                {
                    //Gives a strike
                    GetComponent<KMBombModule>().HandleStrike();
                    Debug.LogFormat("[Brainf--- #{0}] You submitted {1}, when the answer was {2}. Incorrect.", moduleId, stageMesh.text, answers[0]);

                    //The stage number display shows the last 2 digits of the stage number
                    if (solvedModules < 10)
                    {
                        stageMesh.text = "0" + solvedModules.ToString();
                    }
                    else if (solvedModules > 99)
                    {
                        stageMesh.text = (solvedModules % 100).ToString();
                    }
                    else
                    {
                        stageMesh.text = solvedModules.ToString();
                    }
                }
            }
            //Clears the inputed data 
            inputStarted = false;
        }
        else if (!inputStarted)
        {
            //Starts writing the inputted number
            stageMesh.text = number.ToString();
            inputStarted = true;
        }
        else if (stageMesh.text.Length == 1)
        {
            //Writes the second inputted number
            stageMesh.text += number.ToString();
        }
    }

    // Fixed updated calls at a fixed amount regardless of frame rate
    void Update()
    {
        if (moduleSolved) return;
        ticker++;
        if (ticker == 5)
        {
            //Resets ticker
            ticker = 0;

            //Gets the amount of solved modules, removes any ignored modules and compares it to the previous readings to see if anything changed.
            List<string> newSolves = bomb.GetSolvedModuleNames().Where(x => !Ignoreds.Contains(x)).ToList();

            //If the solve count hasn't changed (or an ignored module has been solved), nothing changes
            if (newSolves.Count() == solvedModules)
            {
                return;
            }

            //Updates the solve count and displays
            solvedModules = newSolves.Count();
            symbolMesh.text = script[solvedModules].ToString();

            //The stage number display shows the last 2 digits of the stage number
            if (solvedModules < 10)
            {
                stageMesh.text = "0" + solvedModules.ToString();
            }
            else if (solvedModules > 99)
            {
                stageMesh.text = (solvedModules % 100).ToString();
            }
            else
            {
                stageMesh.text = solvedModules.ToString();
            }

            //When the stage advances without the defuser solving the fullstop, it will give a strike but still progress
            if (fullStopSolved == false)
            {
                GetComponent<KMBombModule>().HandleStrike();
                answers.RemoveAt(0);
                Debug.LogFormat("[Brainf--- #{0}] You continued before solving. Strike!", moduleId);
                fullStopSolved = true;
            }

            //If a fullstop appears then wait for the defuser's input
            if (script[solvedModules] == '.')
            {
                fullStopSolved = false;
            }

            //Moves the text so it is in the centre
            if (script[solvedModules] == '.' | script[solvedModules] == ',')
            {
                symbolMesh.transform.localPosition = new Vector3(0.0f, 0.27f, 2.64f);
            }
            else
            {
                symbolMesh.transform.localPosition = new Vector3(0.0f, 0.27f, 0.04f);
            }
        }
    }
}