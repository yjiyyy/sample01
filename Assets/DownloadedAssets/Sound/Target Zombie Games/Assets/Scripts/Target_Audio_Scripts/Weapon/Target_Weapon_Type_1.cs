// Description : Use this script with sound like chainsaw
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TS.ZombieGames
{
	public class Target_Weapon_Type_1 : MonoBehaviour
	{

		public string State = "Off";
		public GameObject s_Att_1;
		private AudioSource s_att_1;
		public GameObject s_Att_2;
		private AudioSource s_att_2;
		public float pitchImpactOnPart2 = .1f;
		public float PitchSpeed = 2;
		private bool PitchMod_On = false;
		public GameObject s_Att_3;
		private AudioSource s_att_3;

		public AudioClip[] s_audio;
		private AudioSource s_Sound;

		void Start()
		{
			s_att_1 = s_Att_1.GetComponent<AudioSource>();
			s_att_2 = s_Att_2.GetComponent<AudioSource>();
			s_att_3 = s_Att_3.GetComponent<AudioSource>();

			s_Sound = GetComponent<AudioSource>();
		}

		void Update()
		{
			if (Input.GetKeyDown("a") && State == "Off")
			{
				State = "On";
				s_Sound.loop = false;
				s_Sound.clip = s_audio[0];
				s_Sound.Play();
			}

			if (State == "On" && !s_Sound.isPlaying || State == "Att_Part_3" && !s_att_3.isPlaying)
			{                           // Idle Chainsaw
				s_Sound.loop = true;
				s_Sound.clip = s_audio[1];
				s_Sound.Play();
				State = "Idle";
			}

			if (Input.GetKeyDown("a") && (State == "Idle"
				|| State == "Att_Part_1" || State == "Att_Part_2" || State == "Att_Part_3" || State == "Idle_tmp"))
			{           // Stop chainsaw
				State = "Off";
				s_Sound.loop = false;
				s_Sound.clip = s_audio[2];
				s_Sound.Play();
				s_att_1.Stop();
				s_att_2.Stop();
				s_att_3.Stop();
			}

			if (Input.GetKeyDown("q") && (State == "Idle" || State == "Idle_tmp" || State == "Att_Part_3"))
			{                   // -> Attack : Part 1
				State = "Att_Part_1";
				s_att_1.Play();
				s_Sound.Stop();
			}

			if (Input.GetKeyUp("q") && State == "Att_Part_1")
			{                                                               // -> Attack : Part 1 To Idle 1
				s_att_2.Stop();
				s_att_3.Play();
				State = "Idle_tmp";
			}
			if (State == "Idle_tmp" && !s_att_3.isPlaying)
			{                                                                   // -> Attack : Part 1 To Idle 2
				s_Sound.loop = true;
				s_Sound.clip = s_audio[1];
				s_Sound.Play();
				State = "Idle";
			}

			if (Input.GetKey("q") && State == "Att_Part_1" && !s_att_1.isPlaying)
			{                                           // -> Attack : Part 2
				State = "Att_Part_2";
				s_att_2.Play();
				PitchMod_On = true;
			}
			if (Input.GetKeyUp("q") && State == "Att_Part_2" && s_att_2.isPlaying)
			{                                           // -> Attack : Part 2 to Idle
				State = "Att_Part_3";
				s_att_2.Stop();
				s_att_3.Play();
				PitchMod_On = false;
			}

			F_PitchModulation();
		}


		public void F_PitchModulation()
		{                                           // modulate the pitch for part 2
			if (PitchMod_On)
			{
				s_att_2.pitch += Mathf.Sin(Time.time * PitchSpeed) * Time.deltaTime * pitchImpactOnPart2;
			}
			if (!PitchMod_On) s_att_2.pitch = 1;
		}
	}
}