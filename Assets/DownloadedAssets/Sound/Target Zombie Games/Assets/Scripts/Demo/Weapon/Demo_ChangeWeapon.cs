// Description : Demo_ChangeWeapon
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TS.ZombieGames
{
	public class Demo_ChangeWeapon : MonoBehaviour
	{

		public GameObject[] arr_Weapons;
		public int[] arr_Weapons_Instruction;
		public int cmpt = 0;
		public Text txt_WeaponName;
		public Text txt_Instruction;

		void Start()
		{
			if (txt_WeaponName) txt_WeaponName.text = arr_Weapons[cmpt].name;
		}

		void Update()
		{
			if (Input.GetKeyDown("c")) ChangeWeapon();
		}


		public void ChangeWeapon()
		{
			arr_Weapons[cmpt].SetActive(false);
			cmpt++;
			cmpt = cmpt % arr_Weapons.Length;
			arr_Weapons[cmpt].SetActive(true);
			if (txt_WeaponName) txt_WeaponName.text = arr_Weapons[cmpt].name;
			F_txt_Instruction();
		}


		public void F_txt_Instruction()
		{
			if (arr_Weapons_Instruction[cmpt] == 0)
				if (txt_Instruction) txt_Instruction.text = "Press button A to start or stop engine";
			if (arr_Weapons_Instruction[cmpt] == 1)
				if (txt_Instruction) txt_Instruction.text = "Press button A to start or stop engine"
						  + "\n" + "Press button Q to attack";
			if (arr_Weapons_Instruction[cmpt] == 2)
				if (txt_Instruction) txt_Instruction.text = "Press button A to Shoot";
			if (arr_Weapons_Instruction[cmpt] == 3)
				if (txt_Instruction) txt_Instruction.text = "Press button A to Start"
						  + "\n" + "Release button A to Stop";
			if (arr_Weapons_Instruction[cmpt] == 4)
				if (txt_Instruction) txt_Instruction.text = "Press button A";
		}
	}

}
