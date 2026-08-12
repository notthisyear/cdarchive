export const recordSummary = `
    grid-cols-3
    rounded-xl
    p-6
    bg-white
    border-slate-200
    shadow-lg
    hover:bg-slate-100
    dark:bg-slate-800
    dark:border-slate-700
    dark:hover:bg-slate-600/40
    transition-colors
    relative
    group
`

export const recordSummaryEditButton = `
    absolute
    top-4
    right-4
    p-2
    rounded-full
    bg-white/80
    dark:bg-slate-700/80
    shadow
    opacity-0
    scale-90
    group-hover:opacity-100
    group-hover:scale-100
    transition-all
    hover:text-blue-500"
`;

export const trackRow = `
    track-row
    group
    w-full
    flex
    items-center
    justify-between
    px-6
    py-3
    border-slate-200
    dark:border-slate-700
    hover:bg-slate-100
    dark:hover:bg-slate-700/40
    transition-colors
    text-left
`;

export const editorInputBoxLabel = `
    block
    text-sm
    mb-2
    mt-5
`;

export const addRecordInputBoxLabel = `
    block
    text-sm
    font-medium
    text-slate-700
    dark:text-slate-300
    mb-2
`;

export const editorInputBox = `
    w-full
    rounded-lg
    border
    border-slate-300
    dark:border-slate-600
    outline-none
    focus:outline-none
    focus:ring-0
    focus:ring-slate-400
    focus:border-slate-400
    bg-white
    dark:bg-slate-700
    px-3
    py-2
    invalid:[&:not(:placeholder-shown)]:border-red-500
    invalid:[&:not(:placeholder-shown)]:ring-red-500/20
`;

export const buttonPrimary = `
    px-4 py-2
    rounded-lg
    bg-blue-600
    hover:bg-blue-700
    text-white
`;

export const buttonSecondary = `
    px-4 py-2
    rounded-lg
    bg-slate-200
    hover:bg-slate-300
    dark:bg-slate-700
    dark:hover:bg-slate-600
`;

export const buttonDanger = `
    px-4 py-2
    rounded-lg
    bg-red-600
    hover:bg-red-700
    text-white
`;

export const buttonSuccess = `
    px-4 py-2
    rounded-lg
    bg-green-600
    hover:bg-green-700
    text-white
`;

export const autocompleteDropdown = `
    absolute
    top-full
    left-0
    right-0
    mt-1
    rounded-lg
    border
    border-slate-200
    dark:border-slate-700
    bg-white
    dark:bg-slate-800
    shadow-xl
    overflow-hidden
    hidden
    z-50
`;

export const autocompleteDropdownRow = `
    w-full
    flex
    items-center
    gap-4
    px-4
    py-3
    text-left
    hover:bg-slate-100
    dark:hover:bg-slate-700
`;

export const toastNotificationContainer = `
    fixed
    bottom-4
    right-4
    z-[2000]
    flex
    flex-col-reverse
    gap-2
    w-80
    max-w-[calc(100vw-2rem)]
`;

export const toastNotification = `
    bg-white
    dark:bg-slate-800
    text-slate-900
    dark:text-white
    rounded-lg
    shadow-xl
    p-4
    flex
    items-start
    gap-3
    opacity-0
    translate-x-4
    transition-all
    duration-200
    hover:bg-slate-100
    dark:hover:bg-slate-700
`;