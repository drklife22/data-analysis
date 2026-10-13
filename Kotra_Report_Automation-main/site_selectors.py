"""
KOTRA 보고서 생성 화면의 selector를 한곳에서 관리합니다.

아래 값은 1차 구현용 임시 selector입니다. 실제 사이트 DOM이 다르면
README의 codegen 안내를 참고해 이 파일만 교체하면 됩니다.
"""

SELECTORS = {
    "direct_analysis_button": "text=희망 국가 직접 분석",

    "hs_code_input": "input[placeholder*='6자리']",
    "product_name_input": "input#itemName",
    # 사이트 문구가 바뀌어 위 selector가 맞지 않을 때 순서대로 시도합니다.
    "product_name_input_fallbacks": [
        "input[placeholder*='식혜']",
        "input[placeholder*='음료']",
        "xpath=//label[contains(normalize-space(.), '수출품명')]/following::input[1]",
        "xpath=//label[contains(normalize-space(.), '품목명')]/following::input[1]",
        "xpath=//*[self::label or self::p or self::span or self::div][normalize-space(text())='수출품명' or normalize-space(text())='품목명']/following::input[1]",
        "input[placeholder*='품명']",
        "input[placeholder*='품목']",
        "textarea[placeholder*='음료']",
        "textarea[placeholder*='품명']",
        "textarea[placeholder*='품목']",
    ],
    "export_scale_dropdown": "xpath=//label[contains(normalize-space(.), '수출액 규모')]/following-sibling::button[@role='combobox'][1]",
    "export_scale_dropdown_fallbacks": [
        "button[role='combobox']:has-text('수출액 규모')",
        "xpath=//label[contains(normalize-space(.), '수출액 규모')]/following::button[@role='combobox'][1]",
        "button[role='combobox']",
    ],
    # 드롭다운 버튼 옆에 숨겨진 기본 <select> (옵션 value: 내수/초보/유망/성장/선도)
    "export_scale_native_select": "select:has(option[value='초보'])",

    "export_experience_first": "text=처음입니다",
    "export_experience_has": "text=수출 경험 있음",

    "target_country_input": "input[placeholder*='미국']",

    "generate_button": "button:has-text('보고서 생성')",
    "download_button": "button:has-text('PDF 저장')",
    "download_button_fallback": "button:has-text('다운로드')",
    "retry_button": "button:has-text('다시 시도하기')",
    "streaming_error_text": "text=Unexpected server Streaming error occurred",
    "reset_button": "button:has-text('초기화')",
    "new_analysis_button": "button:has-text('새로운 분석 시작하기')",
}
