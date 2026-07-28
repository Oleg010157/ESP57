################################################################################
# Automatically-generated file. Do not edit!
################################################################################

SHELL = cmd.exe

# Each subdirectory must supply rules for building sources it contributes
build-1948446974: ../targetConfigs/c2000.syscfg
	@echo 'SysConfig: "$<"'
	"C:/ti/ccs2100/ccs/utils/sysconfig_1.28.0/sysconfig_cli.bat" --script "C:/Work/K1/INVERTOR_CCS/targetConfigs/c2000.syscfg" -o "syscfg" --compiler ccs
	@echo ' '

syscfg/PinmuxConfigSummary.csv: build-1948446974 ../targetConfigs/c2000.syscfg
syscfg: build-1948446974


