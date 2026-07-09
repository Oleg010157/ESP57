################################################################################
# Automatically-generated file. Do not edit!
################################################################################

SHELL = cmd.exe

# Each subdirectory must supply rules for building sources it contributes
CLA/%.obj: ../CLA/%.cla $(GEN_OPTS) | $(GEN_FILES) $(GEN_MISC_FILES)
	@echo 'C2000 Compiler - building file: "$<"'
	"C:/ti/ccs2100/ccs/tools/compiler/ti-cgt-c2000_25.11.1.LTS/bin/cl2000" -v28 -ml -mt --cla_support=cla2 --float_support=fpu32 --idiv_support=idiv0 --tmu_support=tmu1 --vcu_support=vcu2 -Ooff --fp_mode=relaxed --include_path="C:/Users/M4800/Desktop/P55_Work/K1/empty_bitfield_driverlib" --include_path="C:/ti/C2000Ware_26_01_00_00/device_support/f28p55x/headers/include/" --include_path="C:/Users/M4800/Desktop/P55_Work/K1/empty_bitfield_driverlib/device" --include_path="C:/ti/C2000Ware_26_01_00_00/driverlib/f28p55x/driverlib/" --include_path="C:/ti/ccs2100/ccs/tools/compiler/ti-cgt-c2000_25.11.1.LTS/include" --include_path="C:/ti/C2000Ware_26_01_00_00/device_support/f28p55x/common/include" --include_path="C:/Users/M4800/Desktop/P55_Work/K1/bin" --include_path="C:/Users/M4800/Desktop/P55_Work/K1/empty_bitfield_driverlib/KSP" --include_path="C:/Users/M4800/Desktop/P55_Work/K1/empty_bitfield_driverlib/CLA" --define=CPU1 --define=DUAL_HEADERS --diag_suppress=10063 --diag_warning=225 --diag_wrap=off --display_error_number --abi=eabi --preproc_with_compile --preproc_dependency="CLA/$(basename $(<F)).d_raw" --include_path="C:/Users/M4800/Desktop/P55_Work/K1/empty_bitfield_driverlib/CPU1_RAM/syscfg" --obj_directory="CLA" $(GEN_OPTS__FLAG) "$<"
	@echo 'Finished building: "$<"'
	@echo ' '

CLA/%.obj: ../CLA/%.c $(GEN_OPTS) | $(GEN_FILES) $(GEN_MISC_FILES)
	@echo 'C2000 Compiler - building file: "$<"'
	"C:/ti/ccs2100/ccs/tools/compiler/ti-cgt-c2000_25.11.1.LTS/bin/cl2000" -v28 -ml -mt --cla_support=cla2 --float_support=fpu32 --idiv_support=idiv0 --tmu_support=tmu1 --vcu_support=vcu2 -Ooff --fp_mode=relaxed --include_path="C:/Users/M4800/Desktop/P55_Work/K1/empty_bitfield_driverlib" --include_path="C:/ti/C2000Ware_26_01_00_00/device_support/f28p55x/headers/include/" --include_path="C:/Users/M4800/Desktop/P55_Work/K1/empty_bitfield_driverlib/device" --include_path="C:/ti/C2000Ware_26_01_00_00/driverlib/f28p55x/driverlib/" --include_path="C:/ti/ccs2100/ccs/tools/compiler/ti-cgt-c2000_25.11.1.LTS/include" --include_path="C:/ti/C2000Ware_26_01_00_00/device_support/f28p55x/common/include" --include_path="C:/Users/M4800/Desktop/P55_Work/K1/bin" --include_path="C:/Users/M4800/Desktop/P55_Work/K1/empty_bitfield_driverlib/KSP" --include_path="C:/Users/M4800/Desktop/P55_Work/K1/empty_bitfield_driverlib/CLA" --define=CPU1 --define=DUAL_HEADERS --diag_suppress=10063 --diag_warning=225 --diag_wrap=off --display_error_number --abi=eabi --preproc_with_compile --preproc_dependency="CLA/$(basename $(<F)).d_raw" --include_path="C:/Users/M4800/Desktop/P55_Work/K1/empty_bitfield_driverlib/CPU1_RAM/syscfg" --obj_directory="CLA" $(GEN_OPTS__FLAG) "$<"
	@echo 'Finished building: "$<"'
	@echo ' '


