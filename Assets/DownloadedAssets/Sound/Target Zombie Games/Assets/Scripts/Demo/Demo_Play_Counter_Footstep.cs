// Demo_Play_Counter_Footstep : Description : Use to play footsteps sounds on demo scene
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TS.ZombieGames
{
	public class Demo_Play_Counter_Footstep : MonoBehaviour
	{


		private AudioSource s_Audio;                // Access audioSource component
		[Header("Put your samples here")]

		public AudioClip[] Samples;             // Put here your samples
		public AudioClip BlankSound;

		private float TimeBetweenTwoSamples;                    // Time between to sample is played
		public AudioClip End_Sample;                // this sound is played when variable Counter = 0
		private float TimeBetweenLastSample = .2f;              // Time to wait before playing the last sound
		public float TimeToLoop = 1.5f;             //

		private bool b_TimeToLoop = false;
		private float Timer_TimeToLoop;

		private bool b_TimeBetweenTwoSamples = false;
		//private float Timer_TimeBetweenTwoSamples;

		private bool b_TimeBetweenLastSample = false;
		private float Timer_TimeBetweenLastSample;

		[Header("Random Pitch")]
		public bool RandomPitch = true;         // if false random is deactivate
		public float Pitch_Min = 1;             // Randomize picth when a sound is played. Minimum pitch
		public float Pitch_Max = 1.2f;              // Maximum pitch
		[Header("Random Volume")]
		public bool RandomVolume = true;            // if false random is deactivate
		public float Volume_Min = .8f;              // Randomize volume when a sound is played. Minimum volume
		public float Volume_Max = 1;                // Maximum volume
		private int tmp_Counter = 0;                    // Know which sample is currently playing
		private bool Play_On = false;           // Use for the demo button

		[Header("Counter")]
		public int Counter = 4;                 // How many sound you want to play
		private int tmp_Counter_2;


		private Text txt_Info;                  // use to display which sound is used on a specific sound
		private string[] Tmp_Name;
		private bool ok = false;

		private Demo_Manager obj_Manager;
		private Image ImageColor;
		public GameObject Sprite_Sound;             // Connect a srpite here. Use to visualize that a sound is playing



		void Start()
		{                                                           // -> Init
			s_Audio = GetComponent<AudioSource>();                                  // Access Audio Component
			ImageColor = GetComponent<Image>();
			tmp_Counter_2 = Counter;

			obj_Manager = GameObject.Find("Demo_Manager").GetComponent<Demo_Manager>();

			txt_Info = GameObject.Find("TXT_Info_Name").GetComponent<Text>();
			int tmp_NumberOfSample = 0;                                     // Use for demo to know the name of the sample used
			if (Samples.Length > 0) tmp_NumberOfSample += Samples.Length;
			if (End_Sample) tmp_NumberOfSample++;
			Tmp_Name = new string[tmp_NumberOfSample];
			for (var i = 0; i < Samples.Length; i++)
			{
				ok = false;
				for (var j = 0; j < Tmp_Name.Length; j++)
				{
					if (Samples[i].name == Tmp_Name[j])
					{
						ok = true;
					}
				}
				if (!ok) Tmp_Name[i] = Samples[i].name;
			}
			if (End_Sample) Tmp_Name[Tmp_Name.Length - 1] = End_Sample.name;
		}

		void Update()
		{
			if (b_TimeToLoop)
			{                                       // Use when all the sound is played and you want to restart playing this sounds
				Timer_TimeToLoop = Mathf.MoveTowards(Timer_TimeToLoop, TimeToLoop, Time.deltaTime);                             // Here the timer to know if the leds must be On or Off
				if (Timer_TimeToLoop == TimeToLoop)
				{
					b_TimeToLoop = false;
					b_TimeBetweenTwoSamples = false;
					b_TimeBetweenLastSample = false;
					//Timer_TimeBetweenTwoSamples = 0;
					Timer_TimeBetweenLastSample = 0;
					Timer_TimeToLoop = 0;
					Play_On = true;
					tmp_Counter = 0;
					tmp_Counter_2 = Counter;
					Demo_Button_On();
				}
			}

			if (b_TimeBetweenTwoSamples)
			{                           // Use to wait time before playing a new sounds
				if (BlankSound != null && !s_Audio.isPlaying && s_Audio.clip != BlankSound)
				{
					s_Audio.clip = BlankSound;
					s_Audio.Play();
				}
				else if (BlankSound != null && !s_Audio.isPlaying && s_Audio.clip == BlankSound)
				{
					b_TimeBetweenTwoSamples = false;
					//Timer_TimeBetweenTwoSamples = 0;
					PlayAList();
				}
				else if (BlankSound == null && !s_Audio.isPlaying)
				{
					b_TimeBetweenTwoSamples = false;
					//Timer_TimeBetweenTwoSamples = 0;
					PlayAList();
				}
			}

			if (b_TimeBetweenLastSample)
			{                               // Use to wait time before playing the last sound
				Timer_TimeBetweenLastSample = Mathf.MoveTowards(Timer_TimeBetweenLastSample, TimeBetweenLastSample, Time.deltaTime);                                // Here the timer to know if the leds must be On or Off
				if (Timer_TimeBetweenLastSample == TimeBetweenLastSample)
				{
					b_TimeBetweenLastSample = false;
					Timer_TimeBetweenLastSample = 0;
					PlayEndSound();
				}
			}
		}


		public void RTU_Play()
		{                                                       // -> Play sound on the same order as you put them inside variable Samples
			if (RandomPitch) s_Audio.pitch = Random.Range(Pitch_Min, Pitch_Max);        // Random pitch
			if (RandomVolume) s_Audio.volume = Random.Range(Volume_Min, Volume_Max);    // Radom volume

			if (Samples.Length > 0)
			{                                                   // Play sound
				s_Audio.clip = Samples[tmp_Counter];
				s_Audio.Play();
			}

			tmp_Counter++;
			tmp_Counter = tmp_Counter % Samples.Length;

			if (Counter == -1)
			{
				b_TimeBetweenTwoSamples = true;
			}
			else if (tmp_Counter_2 > 1)
			{                                               // if counter > 1 play sound from variable Samples
				b_TimeBetweenTwoSamples = true;
				tmp_Counter_2--;
			}
			else
			{                                                                   // play end sound
				b_TimeBetweenLastSample = true;
				Play_On = false;
			}

		}


		public void Demo_Button_On()
		{                                               // -> Use on demo scene to start and a sound when you press a button																// Start Playing sound
			Play_On = true;
			tmp_Counter = 0;
			tmp_Counter_2 = Counter;
			RTU_Play();

			if (txt_Info)
			{
				txt_Info.text = gameObject.name + " use : ";
				for (var i = 0; i < Tmp_Name.Length; i++)
				{
					if (Tmp_Name[i] != null)
					{
						txt_Info.text += Tmp_Name[i];
						if (i < Tmp_Name.Length - 1) txt_Info.text += " + ";
					}
				}
			}
			ImageColor.color = new Color(.9f, .9f, .9f, 1);
			if (Sprite_Sound) Sprite_Sound.SetActive(true);
		}

		public void Demo_Button_Off()
		{                                               // -> Use on demo scene to start and a sound when you press a button
			tmp_Counter_2 = 0;
			Play_On = false;
			s_Audio.Stop();
			b_TimeBetweenTwoSamples = false;
			//Timer_TimeBetweenTwoSamples = 0;
			b_TimeToLoop = false;
			Timer_TimeToLoop = 0;

			if (txt_Info)
			{
				txt_Info.text = "Stop Sound";
			}
			ImageColor.color = new Color(1, 1, 1, 1);
			if (Sprite_Sound) Sprite_Sound.SetActive(false);
		}

		public void Demo_StopSound()
		{                                                   // -> use to stop playing a sound
			tmp_Counter_2 = 0;
			Play_On = false;
			s_Audio.Stop();
		}

		public void PlayAList()
		{                                                       // -> use to play sound sound from variable Samples
			if (Play_On) RTU_Play();
		}

		public void PlayEndSound()
		{                                                   // -> Use to play the final sound
			if (End_Sample)
			{
				s_Audio.clip = End_Sample;
				s_Audio.Play();
			}
			b_TimeToLoop = true;
		}

		public void Call_Manager_To_Play_A_Sound()
		{
			obj_Manager.PlayASound(this.name);
		}
	}
}