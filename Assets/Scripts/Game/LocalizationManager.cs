using UnityEngine;
using System.Collections.Generic;

public enum Language { Korean, English, Indonesian, Japanese, Spanish, Portuguese, French, German, Italian, Russian, Vietnamese, Turkish }

/// <summary>
/// 화면 문구 언어 전환 매니저 (싱글턴, PlayerPrefs 저장)
/// </summary>
public class LocalizationManager
{
    static LocalizationManager _instance;
    public static LocalizationManager Instance => _instance ??= new LocalizationManager();

    const string PREF_KEY = "Language";

    public Language CurrentLanguage { get; private set; }

    /// <summary>언어가 바뀔 때 발행 — UI가 구독해 텍스트를 갱신합니다.</summary>
    public System.Action OnLanguageChanged;

    // [0] 한국어 [1] English [2] Indonesia [3] 日本語 [4] Español [5] Português [6] Français
    // [7] Deutsch [8] Italiano [9] Русский [10] Tiếng Việt [11] Türkçe  — enum Language 순서와 같다.
    // 한국어·영어·인도네시아어·러시아어는 SCDream, 나머지는 M PLUS 2 로 그린다(GameFonts).
    // 문구를 고치면 M PLUS 2 부분 폰트도 다시 만들어야 한다 — 새 글자가 빠져 빈칸이 된다.
    // 인도네시아어는 지금 폰트(SCDream)에 있는 기본 알파벳만 쓴다. 악센트 글자(é 등)를 넣으면
    // 폰에서 빈칸이 된다 — 스페인어·프랑스어를 따로 안 넣은 이유이기도 하다.
    static readonly Dictionary<string, string[]> _table = new Dictionary<string, string[]>
    {
        // ── 메인 메뉴 ──────────────────────────────────────────────
        { "start",         new[] { "시작",              "Start", "Mulai", "スタート", "Jugar", "Jogar", "Jouer", "Start", "Gioca", "Играть", "Bắt đầu", "Başla"         } },
        { "no_ads",        new[] { "광고 제거",          "Remove Ads", "Hapus Iklan", "広告を削除", "Quitar anuncios", "Remover anúncios", "Sans pub", "Werbung entfernen", "Rimuovi pubblicità", "Убрать рекламу", "Xóa quảng cáo", "Reklamları kaldır"    } },
        { "lang_btn",      new[] { "한국어",             "ENG", "IND", "日本語", "ESP", "POR", "FRA", "DEU", "ITA", "РУС", "VIE", "TÜR"           } },

        // ── 모드 이름 ──────────────────────────────────────────────
        { "mode_normal",   new[] { "노말",              "Normal", "Normal", "ノーマル", "Normal", "Normal", "Normal", "Normal", "Normale", "Обычный", "Thường", "Normal"        } },
        { "mode_ice",      new[] { "아이스",             "Ice", "Es", "アイス", "Hielo", "Gelo", "Glace", "Eis", "Ghiaccio", "Лёд", "Băng", "Buz"           } },
        { "mode_toggle",   new[] { "토글",              "Toggle", "Toggle", "トグル", "Toggle", "Toggle", "Toggle", "Toggle", "Toggle", "Тоггл", "Toggle", "Toggle"        } },
        { "mode_disco",    new[] { "디스코",            "Disco", "Disko", "ディスコ", "Disco", "Disco", "Disco", "Disco", "Disco", "Диско", "Disco", "Disko"         } },
        { "coming_soon",   new[] { "COMING\nSOON",     "COMING\nSOON", "SEGERA\nHADIR", "近日\n公開", "PRÓXIMA-\nMENTE", "EM\nBREVE", "BIENTÔT\nDISPO", "BALD\nVERFÜGBAR", "IN\nARRIVO", "СКОРО", "SẮP\nRA MẮT", "YAKINDA"  } },

        // ── 잠금 해제 조건 (string.Format: {0}=모드명, {1}=점수) ──
        { "unlock_fmt",    new[] { "{0}모드 최고 점수\n{1}점 이상", "{0} mode high score\n{1}+", "Skor tertinggi mode {0}\n{1}+", "{0}モードの最高スコア\n{1}点以上", "Récord del modo {0}\n{1}+", "Recorde do modo {0}\n{1}+", "Record du mode {0}\n{1}+", "Rekord im Modus {0}\n{1}+", "Record modalità {0}\n{1}+", "Рекорд в режиме {0}\n{1}+", "Kỷ lục chế độ {0}\n{1}+", "{0} modu rekoru\n{1}+" } },
        // 잠긴 모드에서 시작 버튼 자리를 대신한다 — 못 하는 이유만 보여 주는 것보다
        // 지금 할 수 있는 선택을 주는 편이 낫다.
        { "unlock_all",    new[] { "모든 모드 해금",      "Unlock All Modes", "Buka Semua Mode", "全モード解放", "Desbloquear todo", "Desbloquear tudo", "Tout débloquer", "Alle freischalten", "Sblocca tutto", "Открыть все", "Mở khóa tất cả", "Tümünü aç" } },
        // 뒤로가기(안드로이드 백 버튼)로 앱을 나가기 전에 한 번 묻는다.
        { "quit_title",    new[] { "게임을 종료하시겠습니까?", "Quit the game?", "Keluar dari game?", "ゲームを終了しますか？", "¿Salir del juego?", "Sair do jogo?", "Quitter le jeu ?", "Spiel beenden?", "Uscire dal gioco?", "Выйти из игры?", "Thoát trò chơi?", "Oyundan çıkılsın mı?" } },
        { "quit_yes",      new[] { "예",                    "Yes", "Ya", "はい", "Sí", "Sim", "Oui", "Ja", "Sì", "Да", "Có", "Evet"            } },
        { "quit_no",       new[] { "아니오",                 "No", "Tidak", "いいえ", "No", "Não", "Non", "Nein", "No", "Нет", "Không", "Hayır"             } },

        // ── 디버그 패널 ────────────────────────────────────────────
        { "debug_label",   new[] { "DEBUG 최고점수:",   "DEBUG High Score:", "DEBUG Skor Tertinggi:", "DEBUG High Score:", "DEBUG High Score:", "DEBUG High Score:", "DEBUG High Score:", "DEBUG High Score:", "DEBUG High Score:", "DEBUG High Score:", "DEBUG High Score:", "DEBUG High Score:" } },
        { "debug_set",     new[] { "설정",              "Set", "Atur", "Set", "Set", "Set", "Set", "Set", "Set", "Set", "Set", "Set"           } },

        // ── 인게임 (InGameUI에서 사용) ─────────────────────────────
        { "best",          new[] { "최고",              "BEST", "TERBAIK", "ベスト", "MEJOR", "RECORDE", "RECORD", "REKORD", "RECORD", "РЕКОРД", "CAO NHẤT", "EN İYİ"          } },
        { "score",         new[] { "점수",              "SCORE", "SKOR", "スコア", "PUNTOS", "PONTOS", "SCORE", "PUNKTE", "PUNTI", "ОЧКИ", "ĐIỂM", "SKOR"         } },
        { "game_over",     new[] { "게임 오버",          "GAME OVER", "GAME OVER", "ゲームオーバー", "FIN DEL JUEGO", "FIM DE JOGO", "PARTIE FINIE", "GAME OVER", "GAME OVER", "ИГРА ОКОНЧЕНА", "KẾT THÚC", "OYUN BİTTİ"     } },
        // 디스코는 콤보를 놓치는 순간 끝난다. 실패 조건을 그대로 제목으로 쓴다.
        { "game_over_disco", new[] { "리듬을 잃다",        "RHYTHM LOST", "IRAMA HILANG", "リズムが途切れた", "RITMO PERDIDO", "RITMO PERDIDO", "RYTHME PERDU", "TAKT VERLOREN", "RITMO PERSO", "РИТМ ПОТЕРЯН", "MẤT NHỊP", "RİTİM KOPTU"   } },
        { "restart",       new[] { "다시 시작",          "Restart", "Main Lagi", "もう一度", "Reintentar", "Jogar de novo", "Rejouer", "Neustart", "Riprova", "Заново", "Chơi lại", "Yeniden başla"       } },
        { "menu",          new[] { "메뉴",              "Menu", "Menu", "メニュー", "Menú", "Menu", "Menu", "Menü", "Menu", "Меню", "Menu", "Menü"          } },
        { "resume",        new[] { "계속",              "Resume", "Lanjutkan", "再開", "Continuar", "Continuar", "Reprendre", "Weiter", "Riprendi", "Продолжить", "Tiếp tục", "Devam"        } },
        { "pause",         new[] { "일시정지",           "Pause", "Jeda", "一時停止", "Pausa", "Pausa", "Pause", "Pause", "Pausa", "Пауза", "Tạm dừng", "Duraklat"         } },
        { "high_score",    new[] { "최고 점수",          "High Score", "Skor Tertinggi", "最高スコア", "Récord", "Recorde", "Record", "Rekord", "Record", "Рекорд", "Điểm cao nhất", "En yüksek skor"    } },
        { "new_record",    new[] { "신기록!",            "New Record!", "Rekor Baru!", "新記録！", "¡Nuevo récord!", "Novo recorde!", "Nouveau record !", "Neuer Rekord!", "Nuovo record!", "Новый рекорд!", "Kỷ lục mới!", "Yeni rekor!"   } },

        { "revive_ad",     new[] { "▶ 광고 보고 부활",     "▶ Revive (Watch Ad)", "▶ Hidup Lagi (Iklan)", "▶ 広告を見て復活", "▶ Revivir (anuncio)", "▶ Reviver (anúncio)", "▶ Revivre (pub)", "▶ Weiter (Werbung)", "▶ Rivivi (pubblicità)", "▶ Ожить (реклама)", "▶ Hồi sinh (xem QC)", "▶ Canlan (reklam)" } },
        { "cancel",        new[] { "취소",                "Cancel", "Batal", "キャンセル", "Cancelar", "Cancelar", "Annuler", "Abbrechen", "Annulla", "Отмена", "Hủy", "İptal"               } },

        // ── 광고 동의 ─────────────────────────────────────────────
        // 유럽경제지역·영국에서만 보이는 버튼이라 한국어를 볼 일은 사실상 없지만,
        // 그 지역에서 언어를 한국어로 두고 쓰는 사람도 있으므로 비워 두지 않는다.
        { "ad_privacy",    new[] { "광고 설정",            "Ad privacy", "Privasi iklan", "広告設定", "Privacidad de anuncios", "Privacidade de anúncios", "Confidentialité pub", "Werbe-Datenschutz", "Privacy annunci", "Настройки рекламы", "Quyền riêng tư QC", "Reklam gizliliği"           } },

        // ── 최고 점수 공유 카드 ──────────────────────────────────
        { "share_title",   new[] { "최고 점수 공유",        "Share Your Best", "Bagikan Skor Terbaik", "ベストスコアを共有", "Comparte tu récord", "Compartilhe seu recorde", "Partage ton record", "Rekord teilen", "Condividi il record", "Поделиться рекордом", "Chia sẻ kỷ lục", "Rekorunu paylaş"      } },
        { "share_promo_desc", new[] { "카드로 점수를 자랑해 보세요.", "Show off your score with a card.", "Pamerkan skormu dengan kartu.", "カードでスコアを自慢しよう。", "Presume tu puntuación con una tarjeta.", "Mostre sua pontuação com um cartão.", "Montre ton score avec une carte.", "Zeig deinen Punktestand als Karte.", "Mostra il tuo punteggio con una carta.", "Похвастайся счётом с карточкой.", "Khoe điểm của bạn bằng thẻ.", "Skorunu bir kartla göster." } },
        { "share_promo_btn",  new[] { "카드 만들기",        "Make a Card", "Buat Kartu", "カードを作る", "Crear tarjeta", "Criar cartão", "Créer une carte", "Karte erstellen", "Crea carta", "Создать карточку", "Tạo thẻ", "Kart oluştur"          } },
        { "share_promo_later",new[] { "추후 공유로도 만들기 가능합니다.", "You can also make one later from Share.", "Kamu juga bisa membuatnya nanti lewat Bagikan.", "あとから共有でも作れます。", "También puedes crearla luego desde Compartir.", "Você também pode criar depois em Compartilhar.", "Tu peux aussi la créer plus tard via Partager.", "Du kannst sie später auch über Teilen erstellen.", "Puoi crearla anche dopo da Condividi.", "Позже её можно создать через “Поделиться”.", "Bạn cũng có thể tạo sau từ Chia sẻ.", "Daha sonra Paylaş'tan da oluşturabilirsin." } },
        { "share_save",    new[] { "이미지 저장",           "Save Image", "Simpan Gambar", "画像を保存", "Guardar imagen", "Salvar imagem", "Enregistrer", "Bild speichern", "Salva immagine", "Сохранить", "Lưu ảnh", "Görseli kaydet"           } },
        { "share_saving",  new[] { "저장하는 중...",         "Saving...", "Menyimpan...", "保存中...", "Guardando...", "Salvando...", "Enregistrement...", "Speichern...", "Salvataggio...", "Сохранение...", "Đang lưu...", "Kaydediliyor..."            } },
        { "share_saved",   new[] { "사진 앱에 저장했습니다",   "Saved to your photos", "Tersimpan di galeri", "写真に保存しました", "Guardada en tus fotos", "Salva nas suas fotos", "Enregistrée dans tes photos", "In deinen Fotos gespeichert", "Salvata nelle foto", "Сохранено в фото", "Đã lưu vào ảnh", "Fotoğraflara kaydedildi" } },
        { "share_failed",  new[] { "저장하지 못했습니다",     "Could not save", "Gagal menyimpan", "保存できませんでした", "No se pudo guardar", "Não foi possível salvar", "Échec de l'enregistrement", "Speichern fehlgeschlagen", "Impossibile salvare", "Не удалось сохранить", "Không thể lưu", "Kaydedilemedi"       } },
        { "share_denied",  new[] { "저장하려면 사진 저장 권한이 필요합니다",
                                                          "Storage permission is needed to save", "Perlu izin penyimpanan untuk menyimpan", "保存するにはストレージの許可が必要です", "Se necesita permiso de almacenamiento", "É necessária permissão de armazenamento", "Autorisation de stockage requise", "Speicherberechtigung erforderlich", "Serve il permesso di archiviazione", "Нужно разрешение на хранилище", "Cần quyền lưu trữ để lưu", "Kaydetmek için depolama izni gerekli" } },
        { "share_name_hint", new[] { "리더보드에 로그인하면 카드에 내 이름이 들어갑니다",
                                                          "Sign in on the leaderboard to put your name on the card", "Masuk di papan peringkat agar namamu tampil di kartu", "ランキングにログインするとカードに名前が入ります", "Inicia sesión en la clasificación para poner tu nombre", "Entre no ranking para colocar seu nome no cartão", "Connecte-toi au classement pour ajouter ton nom", "Melde dich in der Rangliste an, um deinen Namen zu zeigen", "Accedi alla classifica per mettere il tuo nome", "Войдите в рейтинг, чтобы добавить своё имя", "Đăng nhập bảng xếp hạng để hiện tên trên thẻ", "Adının kartta görünmesi için sıralamaya giriş yap" } },

        // ── 리더보드 ──────────────────────────────────────────────
        { "leaderboard",   new[] { "리더보드",            "Leaderboard", "Papan Peringkat", "ランキング", "Clasificación", "Ranking", "Classement", "Rangliste", "Classifica", "Рейтинг", "Xếp hạng", "Sıralama"          } },
        { "lb_coming_soon",new[] { "추후 출시",            "Coming Soon", "Segera Hadir", "近日公開", "Próximamente", "Em breve", "Bientôt", "Demnächst", "In arrivo", "Скоро", "Sắp ra mắt", "Yakında"          } },
        { "lb_signin_title", new[] { "로그인하고 점수를 뽐내세요",
                                                          "Sign in and show off your score", "Masuk dan pamerkan skormu", "ログインしてスコアを自慢しよう", "Inicia sesión y presume tu puntuación", "Entre e mostre sua pontuação", "Connecte-toi et montre ton score", "Melde dich an und zeig deinen Rekord", "Accedi e mostra il tuo punteggio", "Войдите и покажите свой счёт", "Đăng nhập và khoe điểm", "Giriş yap ve skorunu göster" } },
        { "lb_signin_desc",  new[] { "로그인하면 모드별 순위표에 내 기록이 올라갑니다.\n로그인하지 않아도 게임은 그대로 즐길 수 있습니다.",
                                                          "Sign in to put your record on the mode leaderboards.\nThe game plays just the same without it.", "Masuk agar rekormu tampil di papan peringkat tiap mode.\nTanpa masuk pun game tetap bisa dimainkan.", "ログインするとモード別ランキングに記録が載ります。\nログインしなくてもゲームはそのまま遊べます。", "Inicia sesión para aparecer en las clasificaciones.\nPuedes jugar igual sin hacerlo.", "Entre para aparecer nos rankings de cada modo.\nO jogo funciona igual sem isso.", "Connecte-toi pour figurer au classement de chaque mode.\nLe jeu fonctionne aussi sans.", "Melde dich an, um in den Ranglisten zu erscheinen.\nDas Spiel läuft auch ohne Anmeldung.", "Accedi per comparire nelle classifiche di ogni modalità.\nPuoi giocare anche senza.", "Войдите, чтобы попасть в рейтинги режимов.\nИграть можно и без входа.", "Đăng nhập để ghi tên lên bảng xếp hạng.\nKhông đăng nhập vẫn chơi bình thường.", "Mod sıralamalarına girmek için giriş yap.\nGiriş yapmadan da oynayabilirsin." } },
        { "lb_signin_btn",   new[] { "로그인",              "Sign In", "Masuk", "ログイン", "Iniciar sesión", "Entrar", "Connexion", "Anmelden", "Accedi", "Войти", "Đăng nhập", "Giriş yap"              } },
        { "lb_signin_failed",new[] { "로그인하지 못했습니다", "Could not sign in", "Gagal masuk", "ログインできませんでした", "No se pudo iniciar sesión", "Não foi possível entrar", "Connexion impossible", "Anmeldung fehlgeschlagen", "Accesso non riuscito", "Не удалось войти", "Không thể đăng nhập", "Giriş yapılamadı"    } },
        { "lb_loading",      new[] { "불러오는 중...",       "Loading...", "Memuat...", "読み込み中...", "Cargando...", "Carregando...", "Chargement...", "Laden...", "Caricamento...", "Загрузка...", "Đang tải...", "Yükleniyor..."           } },
        { "lb_empty",        new[] { "아직 기록이 없습니다",  "No scores yet", "Belum ada skor", "まだ記録がありません", "Aún no hay puntuaciones", "Ainda não há pontuações", "Aucun score pour l'instant", "Noch keine Einträge", "Ancora nessun punteggio", "Пока нет результатов", "Chưa có điểm", "Henüz skor yok"        } },
        { "lb_unavailable",  new[] { "이 기기에서는 순위표를 쓸 수 없습니다",
                                                          "Leaderboards are unavailable on this device", "Papan peringkat tidak tersedia di perangkat ini", "この端末ではランキングを利用できません", "Clasificación no disponible en este dispositivo", "Ranking indisponível neste aparelho", "Classement indisponible sur cet appareil", "Rangliste auf diesem Gerät nicht verfügbar", "Classifica non disponibile su questo dispositivo", "Рейтинг недоступен на этом устройстве", "Bảng xếp hạng không khả dụng trên thiết bị này", "Bu cihazda sıralama kullanılamıyor" } },

        // ── 다시 시작 전 광고 제거 제안 ───────────────────────────
        { "iap_title",     new[] { "광고 없이 플레이",     "Play Without Ads", "Main Tanpa Iklan", "広告なしでプレイ", "Juega sin anuncios", "Jogue sem anúncios", "Joue sans pub", "Ohne Werbung spielen", "Gioca senza pubblicità", "Играть без рекламы", "Chơi không quảng cáo", "Reklamsız oyna"     } },
        { "iap_desc",      new[] { "한 번만 구매하면 배너와 전면 광고가 사라집니다",
                                                          "One purchase removes banners and full-screen ads", "Sekali beli, iklan banner dan layar penuh hilang", "一度の購入でバナー広告と全画面広告が消えます", "Una compra elimina los banners y anuncios a pantalla completa", "Uma compra remove banners e anúncios em tela cheia", "Un achat supprime les bannières et les pubs plein écran", "Ein Kauf entfernt Banner- und Vollbildwerbung", "Un acquisto rimuove banner e annunci a schermo intero", "Одна покупка убирает баннеры и полноэкранную рекламу", "Mua một lần để xóa quảng cáo biểu ngữ và toàn màn hình", "Tek satın alma banner ve tam ekran reklamları kaldırır" } },
        { "iap_buy",       new[] { "광고 제거",            "Remove Ads", "Hapus Iklan", "広告を削除", "Quitar anuncios", "Remover anúncios", "Retirer les pubs", "Werbung entfernen", "Rimuovi pubblicità", "Убрать рекламу", "Xóa quảng cáo", "Reklamları kaldır"           } },
        { "iap_later",     new[] { "결제 추후 연결",        "Payment Coming Soon", "Pembayaran Segera Hadir", "決済は近日対応", "Pago próximamente", "Pagamento em breve", "Paiement bientôt", "Zahlung demnächst", "Pagamento in arrivo", "Оплата скоро", "Thanh toán sắp có", "Ödeme yakında"  } },
        { "iap_watch",     new[] { "광고 보고 계속하기",    "Watch Ad & Continue", "Tonton Iklan & Lanjut", "広告を見て続ける", "Ver anuncio y seguir", "Ver anúncio e continuar", "Voir la pub et continuer", "Werbung & weiter", "Guarda e continua", "Смотреть рекламу", "Xem QC và tiếp tục", "Reklam izle ve devam et"  } },
        { "iap_failed",    new[] { "지금은 구매할 수 없습니다",
                                                          "Purchase unavailable right now", "Pembelian belum tersedia saat ini", "現在は購入できません", "Compra no disponible ahora", "Compra indisponível no momento", "Achat indisponible pour le moment", "Kauf derzeit nicht möglich", "Acquisto non disponibile ora", "Покупка сейчас недоступна", "Hiện không thể mua", "Satın alma şu an kullanılamıyor" } },

        // ── 디스코 모드 광과민성 경고 ─────────────────────────────
        // 시작 직후 2초만 떴다 사라지므로 설명은 두 줄을 넘기지 않는다.
        { "photo_warn_title", new[] { "광과민성 발작 주의",  "Photosensitivity Warning", "Peringatan Fotosensitif", "光過敏性発作への注意", "Aviso de fotosensibilidad", "Aviso de fotossensibilidade", "Avertissement photosensibilité", "Warnung: Lichtempfindlichkeit", "Avviso fotosensibilità", "Осторожно: светочувствительность", "Cảnh báo nhạy cảm ánh sáng", "Işığa duyarlılık uyarısı" } },
        { "photo_warn_desc",  new[] { "이 모드에는 밝은 빛과 빠른 색 변화가 있습니다.\n불편함을 느끼면 플레이를 중단해 주세요.",
                                                          "This mode contains flashing lights and\nrapid color changes. Stop playing if you feel unwell.", "Mode ini berisi cahaya berkedip dan\nperubahan warna cepat. Berhenti bermain jika merasa tidak nyaman.", "このモードには強い光と速い色の変化があります。\n気分が悪くなったらプレイを中止してください。", "Este modo tiene luces intermitentes y\ncambios rápidos de color. Deja de jugar si te sientes mal.", "Este modo tem luzes piscantes e\nmudanças rápidas de cor. Pare se não se sentir bem.", "Ce mode contient des flashs lumineux et\ndes changements de couleur rapides. Arrête si tu te sens mal.", "Dieser Modus enthält Lichtblitze und\nschnelle Farbwechsel. Hör auf, wenn dir unwohl ist.", "Questa modalità ha luci lampeggianti e\nrapidi cambi di colore. Smetti se non ti senti bene.", "В этом режиме есть вспышки света и\nбыстрая смена цветов. Прекратите, если стало плохо.", "Chế độ này có ánh sáng nhấp nháy và\nđổi màu nhanh. Hãy dừng nếu thấy khó chịu.", "Bu modda yanıp sönen ışıklar ve\nhızlı renk değişimleri var. Rahatsız olursan bırak." } },

        // ── 인게임 도움말 ─────────────────────────────────────────
        // 규칙 한 줄에 아이콘 하나. 문장이 두 줄을 넘으면 아이콘과 높이가 안 맞으므로
        // 한 줄에 담기는 길이로 쓴다. 영어가 늘 더 길다는 걸 감안해서 자른다.
        { "help_title",    new[] { "게임 방법",            "How to Play", "Cara Bermain", "遊び方", "Cómo jugar", "Como jogar", "Comment jouer", "Spielanleitung", "Come si gioca", "Как играть", "Cách chơi", "Nasıl oynanır"          } },
        { "help_close",    new[] { "닫기",                "Close", "Tutup", "閉じる", "Cerrar", "Fechar", "Fermer", "Schließen", "Chiudi", "Закрыть", "Đóng", "Kapat"                } },

        { "help_drag",     new[] { "조각 세 개를 판 위로 끌어다 놓습니다.",
                                                          "Drag the three pieces onto the board.", "Seret ketiga kepingan ke papan.", "3つのピースを盤面にドラッグして置きます。", "Arrastra las tres piezas al tablero.", "Arraste as três peças para o tabuleiro.", "Fais glisser les trois pièces sur le plateau.", "Zieh die drei Teile aufs Spielfeld.", "Trascina i tre pezzi sul tabellone.", "Перетащите три фигуры на поле.", "Kéo ba khối vào bảng.", "Üç parçayı tahtaya sürükle." } },
        { "help_line",     new[] { "가로나 세로 한 줄을 채우면 지워집니다.",
                                                          "Fill a full row or column to clear it.", "Penuhi satu baris atau kolom untuk menghapusnya.", "横か縦の1列を埋めると消えます。", "Llena una fila o columna para borrarla.", "Preencha uma linha ou coluna para limpá-la.", "Remplis une ligne ou une colonne pour l'effacer.", "Füll eine Reihe oder Spalte, um sie zu löschen.", "Riempi una riga o colonna per cancellarla.", "Заполните ряд или столбец, чтобы убрать его.", "Lấp đầy một hàng hoặc cột để xóa.", "Bir satır veya sütunu doldurup temizle." } },

        { "help_combo",    new[] { "세 조각을 쓰는 동안 한 번도 못 지우면 콤보가 끊깁니다.",
                                                          "Clear at least once per set of three, or the combo resets.", "Hapus minimal sekali tiap set tiga kepingan, atau kombo terputus.", "3つのピースを使う間に一度も消せないとコンボが途切れます。", "Borra al menos una vez cada tres piezas o el combo se reinicia.", "Limpe ao menos uma vez a cada três peças ou o combo zera.", "Efface au moins une fois par série de trois, sinon le combo repart à zéro.", "Lösch mindestens einmal pro drei Teile, sonst endet die Combo.", "Cancella almeno una volta ogni tre pezzi o la combo si azzera.", "Убирайте хотя бы раз за три фигуры, иначе комбо сбросится.", "Mỗi lượt ba khối phải xóa ít nhất một lần, nếu không combo bị mất.", "Her üç parçada en az bir kez temizle, yoksa kombo sıfırlanır." } },

        { "help_ice_slide",new[] { "줄이 지워지면 남은 블록이 한 칸씩 내려옵니다.",
                                                          "After a clear, the blocks above slide down one row.", "Setelah baris terhapus, balok di atasnya turun satu baris.", "列が消えると上のブロックが1マスずつ下がります。", "Tras borrar, los bloques de arriba bajan una fila.", "Depois de limpar, os blocos acima descem uma linha.", "Après un effacement, les blocs au-dessus descendent d'une ligne.", "Nach dem Löschen rutschen die Blöcke darüber eine Reihe nach unten.", "Dopo una cancellazione, i blocchi sopra scendono di una riga.", "После очистки блоки сверху сдвигаются на ряд вниз.", "Sau khi xóa, các khối phía trên trượt xuống một hàng.", "Temizlemeden sonra üstteki bloklar bir sıra aşağı kayar." } },
        { "help_ice_chain",new[] { "내려오다 줄이 또 완성되면 연쇄로 지워집니다.",
                                                          "If that completes another line, it clears too.", "Jika itu melengkapi baris lain, baris itu ikut terhapus.", "下がって列がまたそろうと連鎖で消えます。", "Si eso completa otra línea, también se borra.", "Se isso completar outra linha, ela também some.", "Si ça complète une autre ligne, elle s'efface aussi.", "Entsteht so eine neue Reihe, wird sie auch gelöscht.", "Se si completa un'altra riga, sparisce anche quella.", "Если так соберётся новый ряд, он тоже исчезнет.", "Nếu tạo thành hàng mới, hàng đó cũng bị xóa.", "Bu başka bir satırı tamamlarsa o da temizlenir." } },

        // 토글 — 눈이 뜬 블록이 지금 지울 수 있는 색이다. 게이지는 화면 위의 원 세 개.
        { "help_tg_open",  new[] { "눈을 뜬 블록이 지금 활성화된 색입니다. 이 색으로만 채워진 줄이 지워집니다.",
                                                          "An open-eye block is the active color. Only a line filled with it clears.", "Balok bermata terbuka adalah warna aktif. Hanya baris penuh warna itu yang terhapus.", "目を開けたブロックが今アクティブな色です。その色だけで埋まった列が消えます。", "Un bloque con ojos abiertos es el color activo. Solo se borran líneas llenas de ese color.", "Um bloco de olhos abertos é a cor ativa. Só somem linhas cheias dessa cor.", "Un bloc aux yeux ouverts est la couleur active. Seules les lignes de cette couleur s'effacent.", "Ein Block mit offenen Augen ist die aktive Farbe. Nur Reihen ganz in dieser Farbe verschwinden.", "Un blocco con gli occhi aperti è il colore attivo. Si cancellano solo righe di quel colore.", "Блок с открытыми глазами: активный цвет. Исчезают только ряды из этого цвета.", "Khối mở mắt là màu đang hoạt động. Chỉ hàng toàn màu đó mới bị xóa.", "Gözü açık blok aktif renktir. Yalnızca bu renkle dolu satırlar temizlenir." } },
        { "help_tg_closed",new[] { "눈을 감은 블록은 비활성화된 색입니다. 줄에 섞여 있으면 그 줄은 안 지워집니다.",
                                                          "A closed-eye block is inactive. A line containing one will not clear.", "Balok bermata tertutup tidak aktif. Baris yang berisi balok ini tidak terhapus.", "目を閉じたブロックは非アクティブな色です。列に混ざるとその列は消えません。", "Un bloque con ojos cerrados está inactivo. Una línea que lo tenga no se borra.", "Um bloco de olhos fechados está inativo. Uma linha com ele não some.", "Un bloc aux yeux fermés est inactif. Une ligne qui en contient ne s'efface pas.", "Ein Block mit geschlossenen Augen ist inaktiv. Eine Reihe mit ihm bleibt stehen.", "Un blocco con gli occhi chiusi è inattivo. Una riga che lo contiene resta.", "Блок с закрытыми глазами неактивен. Ряд с ним не исчезнет.", "Khối nhắm mắt không hoạt động. Hàng có nó sẽ không bị xóa.", "Gözü kapalı blok pasiftir. Onu içeren satır temizlenmez." } },
        { "help_tg_gauge", new[] { "화면 위 게이지는 한 줄 지울 때마다 한 칸 찹니다. 세 칸이 다 차면 색이 뒤집히고 스페셜 블록이 나옵니다.",
                                                          "The gauge at the top fills once per clear. At three, the color flips and a special block appears.", "Pengukur di atas terisi tiap kali menghapus. Saat penuh tiga, warna berbalik dan muncul balok spesial.", "上のゲージは1回消すごとに1つたまります。3つたまると色が反転し、スペシャルブロックが出ます。", "La barra de arriba sube con cada borrado. Al llegar a tres, el color cambia y aparece un bloque especial.", "A barra no topo enche a cada limpeza. Em três, a cor inverte e surge um bloco especial.", "La jauge en haut monte à chaque effacement. À trois, la couleur s'inverse et un bloc spécial apparaît.", "Die Leiste oben füllt sich pro Löschung. Bei drei kehrt sich die Farbe um und ein Spezialblock erscheint.", "La barra in alto sale a ogni cancellazione. A tre, il colore si inverte e appare un blocco speciale.", "Шкала сверху растёт с каждой очисткой. На трёх цвет меняется и появляется особый блок.", "Thanh trên cùng tăng mỗi lần xóa. Đủ ba thì màu đảo ngược và xuất hiện khối đặc biệt.", "Üstteki gösterge her temizlemede dolar. Üçte renk döner ve özel blok çıkar." } },
        { "help_tg_swap",  new[] { "스페셜 블록을 줄에 끌어다 놓으면 그 줄이 지금 색이 됩니다.",
                                                          "Drag the special block onto a line to repaint it in the active color.", "Seret balok spesial ke sebuah baris untuk mengecatnya dengan warna aktif.", "スペシャルブロックを列にドラッグすると、その列が今の色になります。", "Arrastra el bloque especial a una línea para pintarla del color activo.", "Arraste o bloco especial para uma linha para pintá-la na cor ativa.", "Fais glisser le bloc spécial sur une ligne pour la peindre de la couleur active.", "Zieh den Spezialblock auf eine Reihe, um sie in der aktiven Farbe einzufärben.", "Trascina il blocco speciale su una riga per colorarla del colore attivo.", "Перетащите особый блок на ряд, чтобы перекрасить его в активный цвет.", "Kéo khối đặc biệt vào một hàng để tô hàng đó màu đang hoạt động.", "Özel bloğu bir satıra sürükleyerek onu aktif renge boya." } },

        // 디스코 — 하트 게이지와 콤보 표시. 콤보 표시는 색이 곧 경고라 두 줄로 나눠 설명한다.
        { "help_dc_gauge", new[] { "화면 위 하트는 줄을 지울 때마다 찹니다. 셋이 다 차면 무지개 블록이 생깁니다.",
                                                          "The hearts at the top fill as you clear lines. At three, a rainbow block appears.", "Hati di atas terisi saat kamu menghapus baris. Saat penuh tiga, muncul balok pelangi.", "上のハートは列を消すたびにたまります。3つたまると虹色ブロックが現れます。", "Los corazones de arriba se llenan al borrar líneas. Con tres aparece un bloque arcoíris.", "Os corações no topo enchem ao limpar linhas. Com três surge um bloco arco-íris.", "Les cœurs en haut se remplissent quand tu effaces. À trois, un bloc arc-en-ciel apparaît.", "Die Herzen oben füllen sich beim Löschen. Bei drei erscheint ein Regenbogenblock.", "I cuori in alto si riempiono cancellando righe. A tre appare un blocco arcobaleno.", "Сердца сверху заполняются при очистке. На трёх появляется радужный блок.", "Các trái tim phía trên đầy dần khi xóa hàng. Đủ ba sẽ xuất hiện khối cầu vồng.", "Üstteki kalpler satır temizledikçe dolar. Üçte gökkuşağı bloğu çıkar." } },
        { "help_dc_rainbow",new[]{ "무지개 블록을 탭하면 그 자리의 가로줄과 세로줄이 함께 지워집니다. 이것도 콤보로 쳐서 못 지운 세트를 넘깁니다.",
                                                          "Tap the rainbow block to clear its row and column at once. That counts as a clear, so the combo carries on.", "Ketuk balok pelangi untuk menghapus baris dan kolomnya sekaligus. Ini dihitung hapus, jadi kombo berlanjut.", "虹色ブロックをタップすると縦横の列が同時に消えます。これも消去扱いでコンボが続きます。", "Toca el bloque arcoíris para borrar su fila y columna. Cuenta como borrado y el combo sigue.", "Toque o bloco arco-íris para limpar sua linha e coluna. Conta como limpeza e o combo continua.", "Touche le bloc arc-en-ciel pour effacer sa ligne et sa colonne. Ça compte, le combo continue.", "Tipp den Regenbogenblock an, um Reihe und Spalte zu löschen. Das zählt, die Combo läuft weiter.", "Tocca il blocco arcobaleno per cancellare riga e colonna. Conta, e la combo continua.", "Нажмите радужный блок, чтобы убрать его ряд и столбец. Это засчитывается, комбо продолжается.", "Chạm khối cầu vồng để xóa cả hàng và cột của nó. Tính là một lần xóa, combo tiếp tục.", "Gökkuşağı bloğuna dokun, satırı ve sütunu birden temizlensin. Sayılır, kombo sürer." } },
        { "help_dc_safe",  new[] { "콤보 표시가 흰색이면 이번 세트에서 이미 한 번 지웠다는 뜻입니다.",
                                                          "A white combo readout means you have already cleared during this set.", "Tanda kombo putih berarti kamu sudah menghapus di set ini.", "コンボ表示が白なら、このセットではすでに一度消しています。", "Si el indicador de combo es blanco, ya borraste en esta tanda.", "Se o indicador de combo estiver branco, você já limpou nesta rodada.", "Si l'indicateur de combo est blanc, tu as déjà effacé dans cette série.", "Ist die Combo-Anzeige weiß, hast du in dieser Runde schon gelöscht.", "Se l'indicatore combo è bianco, hai già cancellato in questo turno.", "Белый индикатор комбо: вы уже убрали ряд в этом наборе.", "Chỉ báo combo màu trắng nghĩa là bạn đã xóa trong lượt này.", "Kombo göstergesi beyazsa bu sette zaten temizledin." } },
        { "help_dc_risk",  new[] { "빨갛게 깜빡이면 아직 못 지운 것입니다. 세 조각을 다 쓰도록 못 지우면 그대로 끝납니다.",
                                                          "Blinking red means you have not cleared yet. Use all three pieces without a clear and the run ends.", "Merah berkedip berarti belum menghapus. Habiskan tiga kepingan tanpa menghapus dan permainan berakhir.", "赤く点滅していたらまだ消していません。3つ使い切っても消せないと終了です。", "Si parpadea en rojo, aún no has borrado. Usa las tres piezas sin borrar y se acaba.", "Se piscar em vermelho, você ainda não limpou. Use as três peças sem limpar e acabou.", "S'il clignote en rouge, tu n'as pas encore effacé. Trois pièces sans effacer et c'est fini.", "Blinkt sie rot, hast du noch nicht gelöscht. Drei Teile ohne Löschen – und vorbei.", "Se lampeggia in rosso non hai ancora cancellato. Tre pezzi senza cancellare e finisce.", "Мигает красным: вы ещё не убирали. Три фигуры без очистки, и конец.", "Nhấp nháy đỏ nghĩa là chưa xóa. Dùng hết ba khối mà không xóa là thua.", "Kırmızı yanıp sönüyorsa henüz temizlemedin. Üç parçayı temizlemeden bitirirsen oyun biter." } },

        // ── 네트워크 연결 안내 ────────────────────────────────────
        { "no_network",    new[] { "인터넷에 연결되어 있지 않습니다.\n네트워크 연결을 확인해 주세요.",
                                                          "No internet connection.\nPlease check your network.", "Tidak ada koneksi internet.\nPeriksa jaringanmu.", "インターネットに接続されていません。\nネットワークを確認してください。", "Sin conexión a internet.\nRevisa tu red.", "Sem conexão com a internet.\nVerifique sua rede.", "Pas de connexion internet.\nVérifie ton réseau.", "Keine Internetverbindung.\nBitte prüfe dein Netzwerk.", "Nessuna connessione internet.\nControlla la rete.", "Нет подключения к интернету.\nПроверьте сеть.", "Không có kết nối internet.\nHãy kiểm tra mạng.", "İnternet bağlantısı yok.\nAğını kontrol et." } },
        { "retry",         new[] { "다시 시도",            "Retry", "Coba Lagi", "再試行", "Reintentar", "Tentar de novo", "Réessayer", "Nochmal", "Riprova", "Повторить", "Thử lại", "Tekrar dene"                } },
        { "checking",      new[] { "확인 중...",           "Checking...", "Memeriksa...", "確認中...", "Comprobando...", "Verificando...", "Vérification...", "Prüfen...", "Verifica...", "Проверка...", "Đang kiểm tra...", "Kontrol ediliyor..."          } },
    };

    LocalizationManager()
    {
        // 직접 고른 적이 있으면 그것, 없으면 기기 언어를 따른다. 예전엔 처음 켜면 늘 한국어였다 —
        // 해외 사용자가 첫 화면부터 읽지 못하는 글자를 보게 된다.
        if (PlayerPrefs.HasKey(PREF_KEY))
            CurrentLanguage = (Language)Mathf.Clamp(PlayerPrefs.GetInt(PREF_KEY, 0), 0, LANG_COUNT - 1);
        else
            CurrentLanguage = FromSystem(Application.systemLanguage);
    }

    const int LANG_COUNT = 12;

    /// <summary>
    /// 언어 고르기 목록에 보일 이름. 각 언어를 그 언어 글자로 쓴다 — 지금 화면 언어를 못 읽는
    /// 사람도 자기 언어는 찾을 수 있어야 한다. 언어를 늘릴 때 여기에 한 줄 더한다.
    /// (일본어·중국어를 넣을 때는 이름 글자 日本語·中文 도 폰트에 있어야 한다 — 지금 폰트엔 한자가 없다)
    /// </summary>
    public static readonly (Language lang, string name)[] Choices =
    {
        (Language.Korean,     "한국어"),
        (Language.English,    "English"),
        (Language.Indonesian, "Bahasa Indonesia"),
        (Language.Japanese,   "日本語"),
        (Language.Spanish,    "Español"),
        (Language.Portuguese, "Português"),
        (Language.French,     "Français"),
        (Language.German,     "Deutsch"),
        (Language.Italian,    "Italiano"),
        (Language.Russian,    "Русский"),
        (Language.Vietnamese, "Tiếng Việt"),
        (Language.Turkish,    "Türkçe"),
    };

    // 지원하는 언어의 기기는 그 언어로, 나머지는 영어로 시작한다(중국어도 아직은 영어).
    static Language FromSystem(SystemLanguage sys)
    {
        switch (sys)
        {
            case SystemLanguage.Korean:     return Language.Korean;
            case SystemLanguage.Indonesian: return Language.Indonesian;
            case SystemLanguage.Japanese:   return Language.Japanese;
            case SystemLanguage.Spanish:    return Language.Spanish;
            case SystemLanguage.Portuguese: return Language.Portuguese;
            case SystemLanguage.French:     return Language.French;
            case SystemLanguage.German:     return Language.German;
            case SystemLanguage.Italian:    return Language.Italian;
            case SystemLanguage.Russian:    return Language.Russian;
            case SystemLanguage.Vietnamese: return Language.Vietnamese;
            case SystemLanguage.Turkish:    return Language.Turkish;
            default:                        return Language.English;
        }
    }

    /// <summary>키에 해당하는 현재 언어 문자열을 반환합니다.</summary>
    public string Get(string key)
    {
        if (_table.TryGetValue(key, out var arr))
        {
            int i = (int)CurrentLanguage;
            return i < arr.Length ? arr[i] : arr[1];   // 번역이 빠진 칸은 영어로
        }
        return key; // 키 미등록 시 키 자체 반환
    }

    public void SetLanguage(Language lang)
    {
        if (CurrentLanguage == lang) return;
        CurrentLanguage = lang;
        PlayerPrefs.SetInt(PREF_KEY, (int)lang);
        PlayerPrefs.Save();
        OnLanguageChanged?.Invoke();
    }
}

/// <summary>
/// 지금 언어에 맞는 폰트. 화면의 모든 글자는 여기서 폰트를 받는다.
///
/// 한국어·영어·인도네시아어·러시아어는 SCDream(한글·기본 알파벳·키릴 문자). 일본어와 악센트가 붙는
/// 유럽 언어(é ñ ü …)·베트남어·터키어는 SCDream 에 글자가 없어 M PLUS 2 로 바꾼다. 빠진 글자만
/// 다른 폰트로 채우면 한 단어 안에서 글꼴이 섞여 보여서, 언어별로 통째로 바꾼다.
///
/// M PLUS 2 는 게임 문구에 쓰는 글자만 남긴 부분 폰트다(원본 1.7MB → 수십 KB). 문구를 바꾸면 다시 만든다.
/// ← ● ○ ★ 같은 기호는 M PLUS 2 에 없으므로, 그런 글자만 찍는 곳은 Symbol 을 쓴다(언어와 상관없이 SCDream).
/// 폰트는 화면을 만들 때 정해지므로 언어를 바꾸면 메인 화면을 다시 연다(MainMenuUI).
/// </summary>
public static class GameFonts
{
    static Font s_scd4, s_scd8, s_mp4, s_mp8;

    static Font Load(ref Font cache, string path)
    {
        if (cache == null) cache = Resources.Load<Font>(path);
        return cache;
    }

    static Font Builtin => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    public static bool UsesMPlus(Language l) =>
        l == Language.Japanese || l == Language.Spanish || l == Language.Portuguese || l == Language.French ||
        l == Language.German   || l == Language.Italian || l == Language.Vietnamese || l == Language.Turkish;

    public static Font For(Language l, bool bold)
    {
        Font f = UsesMPlus(l)
            ? (bold ? Load(ref s_mp8, "Fonts/MPLUS2-Bold") : Load(ref s_mp4, "Fonts/MPLUS2-Regular"))
            : null;
        if (f != null) return f;
        return bold ? (Load(ref s_scd8, "Fonts/SCDream8") ?? Builtin) : (Load(ref s_scd4, "Fonts/SCDream4") ?? Builtin);
    }

    public static Font Regular => For(LocalizationManager.Instance.CurrentLanguage, false);
    public static Font Bold    => For(LocalizationManager.Instance.CurrentLanguage, true);

    /// <summary>← ● ○ ★ 처럼 M PLUS 2 에 없는 기호만 찍는 곳. 언어와 상관없이 SCDream.</summary>
    public static Font SymbolRegular => Load(ref s_scd4, "Fonts/SCDream4") ?? Builtin;
    public static Font SymbolBold    => Load(ref s_scd8, "Fonts/SCDream8") ?? Builtin;
}
