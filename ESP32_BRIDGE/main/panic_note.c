/*
 * panic_note.c
 *
 * След последней паники моста - когда его некому прочитать в COM-порту.
 *
 * 12.11.2026 обновление моста по WiFi раз за разом обрывалось паникой, но
 * только пока COM-порт закрыт: стоило открыть лог - паники не было, и текст
 * её уходить было некуда. Поэтому мост сам кладёт главное о панике в память
 * RTC, которая переживает перезапуск, а после него отдаёт это в /version и в
 * лог: причина и адреса вызовов. Адреса - в имена функций:
 *
 *   xtensa-esp32-elf-addr2line -pfiaC -e build/esp32_bridge.elf 0x400d1234 ...
 *
 * (.elf - именно той сборки, что упала).
 *
 * Перехват - обёрткой линкера вокруг esp_panic_handler (main/CMakeLists.txt,
 * --wrap): сам обработчик паники ESP-IDF не трогаем, он работает дальше как
 * работал.
 */
#include <stdio.h>
#include <string.h>

#include "esp_attr.h"
#include "esp_cpu_utils.h"
#include "esp_debug_helpers.h"
#include "esp_log.h"
#include "esp_private/panic_internal.h"
#include "esp_system.h"
#include "xtensa_context.h"

#include "panic_note.h"

#define PN_MAGIC 0x50414E43u /* 'PANC' */
#define PN_DEPTH 8

typedef struct {
    uint32_t magic;
    uint32_t core, exc, cause, vaddr, n;
    uint32_t pc[PN_DEPTH];
} pn_t;

static RTC_NOINIT_ATTR pn_t s_pn;
static char                s_text[200];

void __real_esp_panic_handler(panic_info_t *info);

/* Зовётся в панике: флеш может быть недоступен - только IRAM и числа, ни
 * одной строки из флеша. */
void IRAM_ATTR __wrap_esp_panic_handler(panic_info_t *info)
{
    const XtExcFrame *f = (const XtExcFrame *)info->frame;

    s_pn.core  = (uint32_t)info->core;
    s_pn.exc   = (uint32_t)info->exception;
    s_pn.cause = f ? (uint32_t)f->exccause : 0;
    s_pn.vaddr = f ? (uint32_t)f->excvaddr : 0;
    s_pn.n     = 0;
    if (f) {
        esp_backtrace_frame_t bt = {
            .pc = (uint32_t)f->pc, .sp = (uint32_t)f->a1, .next_pc = (uint32_t)f->a0, .exc_frame = f};
        s_pn.pc[s_pn.n++] = esp_cpu_process_stack_pc(bt.pc);
        while (s_pn.n < PN_DEPTH && bt.next_pc != 0 && esp_backtrace_get_next_frame(&bt)) {
            s_pn.pc[s_pn.n++] = esp_cpu_process_stack_pc(bt.pc);
        }
    }
    s_pn.magic = PN_MAGIC;

    __real_esp_panic_handler(info);
}

void panic_note_init(void)
{
    s_text[0] = 0;
    if (esp_reset_reason() == ESP_RST_PANIC && s_pn.magic == PN_MAGIC && s_pn.n <= PN_DEPTH) {
        /* exc - panic_exception_t: 0 отладчик, 1 сторож прерываний, 2 сторож
         * задач, 3 abort(), 4 исключение процессора (cause - EXCCAUSE). */
        int len = snprintf(s_text, sizeof s_text, "ядро %lu, вид %lu, EXCCAUSE %lu, EXCVADDR 0x%08lx, PC",
                           (unsigned long)s_pn.core, (unsigned long)s_pn.exc, (unsigned long)s_pn.cause,
                           (unsigned long)s_pn.vaddr);
        for (uint32_t i = 0; i < s_pn.n && len > 0 && len < (int)sizeof s_text; i++) {
            len += snprintf(s_text + len, sizeof s_text - len, " 0x%08lx", (unsigned long)s_pn.pc[i]);
        }
        ESP_LOGW("panic", "прошлый перезапуск - паника: %s", s_text);
    }
    s_pn.magic = 0;
}

const char *panic_note(void)
{
    return s_text;
}
