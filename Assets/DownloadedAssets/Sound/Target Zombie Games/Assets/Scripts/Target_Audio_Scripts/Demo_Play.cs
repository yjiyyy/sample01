using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TS.ZombieGames
{
	public class Demo_Play : MonoBehaviour
	{
		private AudioSource s_Audio;                // Access audioSource component
		[Header("Put your samples here")]
		public AudioClip[] Samples;             // Put here your samples
		[Header("Random Pitch")]
		public bool RandomPitch = true;         // if false random is deactivate
		public float Pitch_Min = 1;             // Randomize picth when a sound is played. Minimum pitch
		public float Pitch_Max = 1.2f;              // Maximum pitch
		[Header("Random Volume")]
		public bool RandomVolume = true;            // if false random is deactivate
		public float Volume_Min = .8f;              // Randomize volume when a sound is played. Minimum volume
		public float Volume_Max = 1;                // Maximum volume
		private int tmp_Counter = 0;                    // Know which sample is currently playing
		[Header("Enable Loop")]
		public bool loop_Mode = false;          // if you want to loop the sound
		private bool Play_On = false;           // Use for the demo button
		private Text txt_Info;
		private Image ImageColor;
		public GameObject Sprite_Sound;

		private Demo_Manager obj_Manager;


		void Start()
		{                                                           // -> Init
			s_Audio = GetComponent<AudioSource>();                                  // Access Audio Component
			ImageColor = GetComponent<Image>();
			if (loop_Mode) s_Audio.loop = true;


			obj_Manager = GameObject.Find("Demo_Manager").GetComponent<Demo_Manager>();

			txt_Info = GameObject.Find("TXT_Info_Name").GetComponent<Text>();
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
		}

		public void Demo_Button_On_Off()
		{                                               // -> Use on demo scene to start and a sound when you press a button
			if (loop_Mode)
			{
				if (Play_On && s_Audio.isPlaying) { Play_On = false; s_Audio.Stop(); }
				else { Play_On = true; RTU_Play(); }
			}
			else
			{
				RTU_Play();
			}

		}

		public void Demo_Button_On()
		{                                               // -> Use on demo scene to start and a sound when you press a button																// Start Playing sound
			if (loop_Mode)
			{
				RTU_Play();
				Play_On = true;
			}
			else
			{
				RTU_Play();
			}
			if (txt_Info)
			{
				txt_Info.text = gameObject.name + " use : " + gameObject.name;
			}
			ImageColor.color = new Color(.9f, .9f, .9f, 1);
			if (Sprite_Sound) Sprite_Sound.SetActive(true);
		}

		public void Demo_Button_Off()
		{                                               // -> Use on demo scene to start and a sound when you press a button
			if (loop_Mode)
			{
				s_Audio.Stop();
				Play_On = false;
			}
			else
			{
				RTU_Play();
			}
			if (txt_Info)
			{
				txt_Info.text = "Stop Sound";

			}
			ImageColor.color = new Color(1, 1, 1, 1);
			if (Sprite_Sound) Sprite_Sound.SetActive(false);
		}

		public void Call_Manager_To_Play_A_Sound()
		{
			obj_Manager.PlayASound(this.name);
		}
	}
}