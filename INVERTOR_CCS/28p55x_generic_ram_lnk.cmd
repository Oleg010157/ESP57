//###########################################################################
//
// FILE:   28p55x_generic_ram_lnk.cmd
//
// TITLE:  Linker command file for the RAM build configurations
//         (CPU1_RAM / CPU1_LAUNCHXL_RAM) of the F28P55x invertor project.
//
// NOTE:   The whole file is guarded by _RAM_BUILD, which is passed to the
//         linker (--define=_RAM_BUILD) only in the RAM build configurations.
//         In the FLASH configurations the file preprocesses to nothing, so it
//         can stay part of the build for every configuration while
//         28p55x_generic_flash_lnk.cmd provides the flash memory map.
//
//         The section placement mirrors 28p55x_generic_flash_lnk.cmd; only
//         the sections that live in flash there (.text, .const, .cinit,
//         .switch, .init_array and the load image of .TI.ramfunc) were moved
//         into RAM:
//
//           RAMGS1..RAMGS3 + RAMLS8..RAMLS9  (0xE000..0x17FFF, 0xA000 words)
//                                            -> .text
//           RAMGS0                           -> .const, ramgs0..ramgs2,
//                                               .TI.ramfunc (run in place,
//                                               no flash-to-RAM copy needed)
//           RAMLS7                           -> .cinit, .switch, .init_array
//
//         .TI.ramfunc sits in RAMGS0, NOT in a RAMLS block, on purpose: the
//         CLA can only reach RAMLS memory, so every LS block is kept free as
//         headroom for the growing CLA program/data. RAMGS is CPU/DMA only and
//         costs the CLA nothing. It also no longer fits one LS block anyway -
//         the section is 0x83a words while a RAMLS block is 0x800. Splitting it
//         across two LS blocks would have worked (largest indivisible input
//         section is 0x38e words, 1ph_Sin.obj .TI.ramfunc:retain), but that
//         spends LS the CLA will want. Headroom left in RAMGS0: 0x54e words.
//
//         Everything else (stack, .bss/.data, the CLA sections and the CLA
//         message RAMs) keeps the same placement as in the flash build.
//
//###########################################################################

#if defined(_RAM_BUILD)

MEMORY
{
   // "Boot to SARAM" entry point - the debugger loads the image and starts
   // at code_start placed here.
   BEGIN            : origin = 0x000000, length = 0x000002

   BOOT_RSVD        : origin = 0x000002, length = 0x000126     /* Part of M0, BOOT rom will use this for stack */
   RAMM0            : origin = 0x000128, length = 0x0002D8
   RAMM1            : origin = 0x000400, length = 0x000400

   RAMLS0           : origin = 0x008000, length = 0x000800
   RAMLS1           : origin = 0x008800, length = 0x000800
   RAMLS2           : origin = 0x009000, length = 0x000800
   RAMLS3           : origin = 0x009800, length = 0x000800
   RAMLS4           : origin = 0x00A000, length = 0x000800
   RAMLS5           : origin = 0x00A800, length = 0x000800
   RAMLS6           : origin = 0x00B000, length = 0x000800
   RAMLS7           : origin = 0x00B800, length = 0x000800
   RAMLS8           : origin = 0x014000, length = 0x002000  // When configured as CLA program use the address 0x4000
   RAMLS9           : origin = 0x016000, length = 0x002000  // When configured as CLA program use the address 0x6000

   // RAMLS8_CLA    : origin = 0x004000, length = 0x002000  // Use only if configured as CLA program memory
   // RAMLS9_CLA    : origin = 0x006000, length = 0x002000  // Use only if configured as CLA program memory

   RAMGS0           : origin = 0x00C000, length = 0x002000
   RAMGS1           : origin = 0x00E000, length = 0x002000
   RAMGS2           : origin = 0x010000, length = 0x002000
   RAMGS3           : origin = 0x012000, length = 0x002000

   CLATOCPURAM      : origin = 0x001480,   length = 0x000080
   CPUTOCLARAM      : origin = 0x001500,   length = 0x000080
   CLATODMARAM      : origin = 0x001680,   length = 0x000080
   DMATOCLARAM      : origin = 0x001700,   length = 0x000080

   RESET            : origin = 0x3FFFC0, length = 0x000002
}


SECTIONS
{
   codestart        : > BEGIN
   .text            : >> RAMGS1 | RAMGS2 | RAMGS3 | RAMLS8 | RAMLS9
   .cinit           : > RAMLS7, ALIGN(8)
   .switch          : > RAMLS7, ALIGN(8)
   .reset           : > RESET, TYPE = DSECT /* not used, */

   .stack           : > RAMM1
#if defined(__TI_EABI__)
   .bss             : > RAMLS5
   .bss:output      : > RAMLS3
   .init_array      : > RAMLS7, ALIGN(8)
   .const           : > RAMGS0, ALIGN(8)
   .data            : > RAMLS6 | RAMLS5
   .sysmem          : > RAMLS4
#else
   .pinit           : > RAMLS7, ALIGN(8)
   .ebss            : >> RAMLS5 | RAMLS6
   .econst          : > RAMGS0, ALIGN(8)
   .esysmem         : > RAMLS5
#endif

   ramgs0 : > RAMGS0
   ramgs1 : > RAMGS0
   ramgs2 : > RAMGS0

   /* Runs in place - in a RAM build there is nothing to copy, so the
      Ramfuncs* load/run symbols of the flash build are not needed. */
   .TI.ramfunc : {} > RAMGS0, ALIGN(8)

   /* CLA specific sections */
   Cla1Prog        : > RAMLS1
   .scratchpad     : > RAMLS2
   .bss_cla        : > RAMLS2
   cla_shared      : > RAMLS2, ALIGN(4)
   .const_cla      : > RAMLS7

   Cla1ToCpuMsgRAM : > CLATOCPURAM
   CpuToCla1MsgRAM : > CPUTOCLARAM
}

#endif // _RAM_BUILD

/*
//===========================================================================
// End of file.
//===========================================================================
*/
