using Gallop;

// Business inputs from the dedicated Legend and Ramen smoke fixtures.
static class PackageLegendFixture
{
    public static SingleModeLegendCheckEventResponse CreateLegendCheckEventResponse(
        int singleModeCharaId,
        int turn)
        => new()
        {
            data = new()
            {
                chara_info = CreateChara(singleModeCharaId, turn),
                home_info = new() { command_info_array = BaseTrainingCommands() },
                unchecked_event_array = [],
                legend_data_set = CreateLegendDataSet(),
                select_index_info_array = []
            }
        };

    public static SingleModeLegendLoadResponse CreateLegendLoadResponse()
        => new()
        {
            data = new()
            {
                single_mode_load_common = new()
                {
                    chara_info = CreateChara(),
                    home_info = new() { command_info_array = BaseTrainingCommands() },
                    unchecked_event_array = [],
                    race_start_info = null
                },
                legend_data_set = CreateLegendDataSet()
            }
        };

    public static SingleModeChara CreateChara(int singleModeCharaId = 0, int turn = 2)
        => new()
        {
            single_mode_chara_id = singleModeCharaId,
            speed = 100,
            stamina = 110,
            power = 120,
            guts = 130,
            wiz = 140,
            max_speed = 1500,
            max_stamina = 1500,
            max_power = 1500,
            max_guts = 1500,
            max_wiz = 1500,
            vital = 80,
            max_vital = 100,
            motivation = 5,
            turn = turn,
            skill_point = 100,
            state = 1,
            playing_state = 1,
            support_card_array = [new() { position = 1, support_card_id = 10001 }],
            evaluation_info_array = [new() { target_id = 1, evaluation = 80 }],
            training_level_info_array = [.. BaseTrainIds().Select(commandId => new TrainingLevelInfo { command_id = commandId, level = 1 })],
            chara_effect_id_array = []
        };

    public static SingleModeCommandInfo[] BaseTrainingCommands()
        => [.. BaseTrainIds().Select((commandId, index) => new SingleModeCommandInfo
        {
            command_type = 1,
            command_id = commandId,
            is_enable = 1,
            training_partner_array = commandId == 101 ? [1] : [],
            tips_event_partner_array = [],
            params_inc_dec_info_array = TrainingParams(index, includeVital: true),
            failure_rate = index * 5,
            sub_command_partner_array = []
        })];

    public static SingleModeLegendDataSet CreateLegendDataSet()
        => new()
        {
            command_info_array = [.. BaseTrainIds().Select((commandId, index) => new SingleModeLegendCommandInfo
            {
                command_type = 1,
                command_id = commandId,
                legend_id = 9046 + (index % 3),
                gain_gauge = index + 1,
                params_inc_dec_info_array = TrainingParams(index + 10, includeVital: false),
                friend_gauge_gain_array = []
            })],
            evaluation_info_array = [],
            gauge_count_array =
            [
                new() { legend_id = 9046, count = 2 },
                new() { legend_id = 9047, count = 4 },
                new() { legend_id = 9048, count = 6 },
            ],
            buff_info_array =
            [
                new() { buff_id = 1101, is_active = 1 },
                new() { buff_id = 2101, is_active = 1 },
                new() { buff_id = 3101, is_active = 0 },
            ],
            obtainable_buff_id_array = [1101],
            masterly_bonus_info = new(),
            race_history_array = [],
            activated_buff_id_array = []
        };

    public static SingleModeParamsIncDecInfo[] TrainingParams(int seed, bool includeVital)
    {
        var targetTypes = includeVital ? new[] { 1, 2, 3, 4, 5, 30, 10 } : new[] { 1, 2, 3, 4, 5, 30 };
        return [.. targetTypes.Select((targetType, index) => new SingleModeParamsIncDecInfo
        {
            target_type = targetType,
            value = targetType == 10 ? -10 : seed + index + 1
        })];
    }

    public static int[] BaseTrainIds() => [101, 105, 102, 103, 106];

    public static void WriteLegendBuffCsv()
    {
        var dataDirectory = Path.Combine("PluginData", "LegendScenarioAnalyzer");
        Directory.CreateDirectory(dataDirectory);
        File.WriteAllText(
            Path.Combine(dataDirectory, "legend_buff.csv"),
            """
            cn_effect,name,rank,color,condition,buffId,isTrigger,isPerson,youQing,ganJing,xunLian,hintLv,hintCount,deYiLv,buZaiLv,jiBan,vitalCostDrop,fenShen,mood,note
            测试蓝效果,测试蓝心得,1,0,201,1001,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
            测试蓝2效果,测试蓝2心得,2,0,201,1002,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
            真实蓝1效果,真实蓝1心得,1,0,201,1101,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
            真实蓝2效果,真实蓝2心得,2,0,201,1102,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
            真实蓝4效果,真实蓝4心得,4,0,201,1104,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
            测试绿效果,测试绿心得,1,1,201,2001,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
            测试绿2效果,测试绿2心得,2,1,201,2002,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
            真实绿1效果,真实绿1心得,1,1,201,2101,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
            真实绿2效果,真实绿2心得,2,1,201,2102,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
            真实绿4效果,真实绿4心得,4,1,201,2104,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
            测试红效果,测试红心得,1,2,201,3001,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
            测试红2效果,测试红2心得,2,2,201,3002,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
            真实红1效果,真实红1心得,1,2,201,3101,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
            真实红3效果,真实红3心得,3,2,201,3103,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
            真实红4效果,真实红4心得,4,2,201,3104,false,false,0,0,0,0,0,0,0,0,0,0,0,smoke
            """);
    }


}
