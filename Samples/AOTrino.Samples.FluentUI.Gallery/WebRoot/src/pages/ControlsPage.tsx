import { useState } from "react";
import {
    Accordion,
    AccordionHeader,
    AccordionItem,
    AccordionPanel,
    Avatar,
    AvatarGroup,
    AvatarGroupItem,
    Badge,
    Body1,
    Button,
    Caption1,
    Checkbox,
    Combobox,
    CompoundButton,
    CounterBadge,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
    Divider,
    Dropdown,
    Field,
    Input,
    Label,
    Link,
    Menu,
    MenuItem,
    MenuList,
    MenuPopover,
    MenuTrigger,
    MessageBar,
    MessageBarActions,
    MessageBarBody,
    MessageBarTitle,
    Option,
    Persona,
    Popover,
    PopoverSurface,
    PopoverTrigger,
    ProgressBar,
    Radio,
    RadioGroup,
    Skeleton,
    SkeletonItem,
    Slider,
    SpinButton,
    Spinner,
    Subtitle2,
    Switch,
    Tab,
    TabList,
    Table,
    TableBody,
    TableCell,
    TableCellLayout,
    TableHeader,
    TableHeaderCell,
    TableRow,
    Tag,
    TagGroup,
    Text,
    Textarea,
    Toast,
    ToastBody,
    ToastTitle,
    Toaster,
    ToggleButton,
    Toolbar,
    ToolbarButton,
    ToolbarDivider,
    Tooltip,
    Tree,
    TreeItem,
    TreeItemLayout,
    makeStyles,
    tokens,
    useId,
    useToastController,
} from "@fluentui/react-components";
import type { SelectTabData } from "@fluentui/react-components";
import {
    TextBold24Regular,
    Calendar24Regular,
    DocumentRegular,
    EditRegular,
    FolderRegular,
    TextItalic24Regular,
    PeopleRegular,
    TextUnderline24Regular,
} from "@fluentui/react-icons";
import { Example } from "../Example";
import { VirtualTable } from "../VirtualTable";
import { Page } from "./Page";

const useStyles = makeStyles({
    subnav: {
        marginBottom: tokens.spacingVerticalM,
    },
    stack: {
        display: "flex",
        flexDirection: "column",
        rowGap: tokens.spacingVerticalM,
        width: "100%",
    },
    row: {
        display: "flex",
        flexWrap: "wrap",
        alignItems: "center",
        columnGap: tokens.spacingHorizontalM,
        rowGap: tokens.spacingVerticalS,
    },
    grow: {
        width: "100%",
    },
});

const categories = [
    { value: "basics", label: "Basics", render: () => <Basics /> },
    { value: "inputs", label: "Inputs", render: () => <Inputs /> },
    { value: "collections", label: "Collections", render: () => <Collections /> },
    { value: "feedback", label: "Feedback", render: () => <Feedback /> },
    { value: "surfaces", label: "Surfaces", render: () => <Surfaces /> },
];

export function ControlsPage() {
    const styles = useStyles();
    const [category, setCategory] = useState("basics");
    const current = categories.find(c => c.value === category) ?? categories[0];

    return (
        <Page
            title="Controls"
            lead={
                <>
                    None of this is AOTrino — it's Fluent UI, rendering in a WebView2 that happens to be a
                    Windows window. That's the point of the whole exercise: the desktop shell is native and
                    tiny, and the widgets are the web's, so you get a component library the size of the web's
                    instead of one the size of a desktop framework's.
                </>
            }
        >
            <TabList
                className={styles.subnav}
                selectedValue={category}
                onTabSelect={(_, d: SelectTabData) => setCategory(d.value as string)}
            >
                {categories.map(c => (
                    <Tab key={c.value} value={c.value}>
                        {c.label}
                    </Tab>
                ))}
            </TabList>
            {current.render()}
        </Page>
    );
}

function Basics() {
    const styles = useStyles();
    return (
        <>
            <Example
                title="Button"
                description="Appearances, not colours: each one is a role, and the theme decides what it looks like."
                code={`<Button appearance="primary">Primary</Button>
<Button appearance="outline">Outline</Button>
<Button icon={<EditRegular />}>With icon</Button>
<ToggleButton>Toggle</ToggleButton>`}
            >
                <Button appearance="primary">Primary</Button>
                <Button>Default</Button>
                <Button appearance="outline">Outline</Button>
                <Button appearance="subtle">Subtle</Button>
                <Button appearance="transparent">Transparent</Button>
                <Button icon={<EditRegular />}>With icon</Button>
                <Button disabled>Disabled</Button>
                <ToggleButton>Toggle</ToggleButton>
                <CompoundButton secondaryContent="and a second line">Compound</CompoundButton>
            </Example>

            <Example
                title="Text"
                description="A type ramp, so sizes are a scale rather than a pile of pixel values."
                code={`<Subtitle2>Subtitle</Subtitle2>
<Body1>Body</Body1>
<Caption1>Caption</Caption1>
<Text font="monospace">Monospace</Text>`}
            >
                <div className={styles.stack}>
                    <Subtitle2>Subtitle2 — a section heading</Subtitle2>
                    <Body1>Body1 — the paragraph you're reading.</Body1>
                    <Caption1>Caption1 — the small print.</Caption1>
                    <Text font="monospace">Text font="monospace" — for the things that must line up.</Text>
                </div>
            </Example>

            <Example
                title="Badge, Tag, Avatar"
                code={`<Badge appearance="tint" color="brand">brand</Badge>
<CounterBadge count={42} />
<Tag>Tag</Tag>
<Avatar name="Ada Lovelace" />
<Persona name="Ada Lovelace" secondaryText="Available" presence={{ status: "available" }} />`}
            >
                <div className={styles.stack}>
                    <div className={styles.row}>
                        <Badge appearance="filled">filled</Badge>
                        <Badge appearance="tint" color="brand">brand</Badge>
                        <Badge appearance="outline" color="success">success</Badge>
                        <Badge appearance="ghost" color="danger">danger</Badge>
                        <CounterBadge count={42} />
                        <CounterBadge count={999} overflowCount={99} />
                    </div>
                    <div className={styles.row}>
                        <TagGroup>
                            <Tag>Plain</Tag>
                            <Tag appearance="brand" icon={<DocumentRegular />}>With icon</Tag>
                            <Tag dismissible>Dismissible</Tag>
                        </TagGroup>
                    </div>
                    <div className={styles.row}>
                        <Avatar name="Ada Lovelace" />
                        <Avatar name="Grace Hopper" badge={{ status: "available" }} />
                        <Avatar icon={<PeopleRegular />} />
                        <AvatarGroup>
                            <AvatarGroupItem name="Ada Lovelace" />
                            <AvatarGroupItem name="Grace Hopper" />
                            <AvatarGroupItem name="Katherine Johnson" />
                        </AvatarGroup>
                        <Persona name="Ada Lovelace" secondaryText="Available" presence={{ status: "available" }} />
                    </div>
                </div>
            </Example>

            <Example
                title="Divider and Link"
                code={`<Divider>or</Divider>
<Link href="#">A link</Link>`}
            >
                <div className={styles.stack}>
                    <Divider>or</Divider>
                    <div className={styles.row}>
                        <Link href="#">A link</Link>
                        <Link appearance="subtle" href="#">Subtle</Link>
                        <Link disabled href="#">Disabled</Link>
                    </div>
                </div>
            </Example>
        </>
    );
}

function Inputs() {
    const styles = useStyles();
    const [slider, setSlider] = useState(40);
    return (
        <>
            <Example
                title="Input and Textarea"
                description="Field wraps a control with its label, hint and validation state — the part everyone re-invents."
                code={`<Field label="Name" hint="As it appears on your passport">
    <Input placeholder="Ada Lovelace" />
</Field>

<Field label="Broken" validationState="error" validationMessage="Something is wrong">
    <Input defaultValue="oops" />
</Field>`}
            >
                <div className={styles.stack}>
                    <Field label="Name" hint="As it appears on your passport">
                        <Input placeholder="Ada Lovelace" />
                    </Field>
                    <Field label="With a validation error" validationState="error" validationMessage="Something is wrong">
                        <Input defaultValue="oops" />
                    </Field>
                    <Field label="Notes">
                        <Textarea placeholder="Longer text goes here" />
                    </Field>
                </div>
            </Example>

            <Example
                title="Checkbox, Radio, Switch"
                code={`<Checkbox label="Checked" defaultChecked />
<Checkbox label="Mixed" checked="mixed" />
<RadioGroup layout="horizontal">
    <Radio value="a" label="A" />
</RadioGroup>
<Switch label="Switch" />`}
            >
                <div className={styles.stack}>
                    <div className={styles.row}>
                        <Checkbox label="Unchecked" />
                        <Checkbox label="Checked" defaultChecked />
                        <Checkbox label="Mixed" checked="mixed" />
                        <Checkbox label="Disabled" disabled />
                    </div>
                    <RadioGroup layout="horizontal" defaultValue="b">
                        <Radio value="a" label="First" />
                        <Radio value="b" label="Second" />
                        <Radio value="c" label="Third" />
                    </RadioGroup>
                    <div className={styles.row}>
                        <Switch label="Off" />
                        <Switch label="On" defaultChecked />
                    </div>
                </div>
            </Example>

            <Example
                title="Slider and SpinButton"
                code={`<Slider value={value} onChange={(_, d) => setValue(d.value)} />
<SpinButton defaultValue={10} min={0} max={100} step={5} />`}
            >
                <div className={styles.stack}>
                    <div className={styles.row}>
                        <Slider min={0} max={100} value={slider} onChange={(_, d) => setSlider(d.value)} />
                        <Text>{slider}</Text>
                    </div>
                    <div className={styles.row}>
                        <Label>Quantity</Label>
                        <SpinButton defaultValue={10} min={0} max={100} step={5} />
                    </div>
                </div>
            </Example>

            <Example
                title="Dropdown and Combobox"
                description="Dropdown picks from a list; Combobox lets you type. Both are listbox-correct with a keyboard, which is the hard part."
                code={`<Dropdown placeholder="Pick one">
    <Option>Segoe UI</Option>
    <Option>Cascadia Code</Option>
</Dropdown>

<Combobox placeholder="Type to filter" freeform>
    <Option>Segoe UI</Option>
</Combobox>`}
            >
                <Field label="Font">
                    <Dropdown placeholder="Pick one">
                        {fonts.map(font => (
                            <Option key={font}>{font}</Option>
                        ))}
                    </Dropdown>
                </Field>
                <Field label="Search">
                    <Combobox placeholder="Type to filter" freeform>
                        {fonts.map(font => (
                            <Option key={font}>{font}</Option>
                        ))}
                    </Combobox>
                </Field>
            </Example>
        </>
    );
}

// deliberately inert sample data: nothing on this page should look like it drives the window
const fonts = ["Segoe UI", "Segoe UI Variable", "Cascadia Code", "Consolas"];

const files = [
    { name: "AOTrino.csproj", kind: "Project", modified: "2 hours ago", icon: <DocumentRegular /> },
    { name: "SystemInfo.cs", kind: "C#", modified: "yesterday", icon: <DocumentRegular /> },
    { name: "npm", kind: "Folder", modified: "last week", icon: <FolderRegular /> },
];

function Collections() {
    const styles = useStyles();
    return (
        <>
            <Example
                title="Half a million rows, over the bridge"
                description={
                    <>
                        The table lives in .NET — 500,000 rows that are never sent, and that the browser is never
                        asked to hold. Scroll it: the page works out which rows are on screen, asks for those
                        200 at a time, and forgets them again. Watch the counter — you can scroll for a while and
                        still have fetched a fraction of a percent of the table. This is what a host object is
                        for; the <code>ping()</code> on the Bridge page is the same mechanism doing nothing
                        interesting.
                    </>
                }
                code={`// C#: rows are computed per request, no table is ever built
public int RowCount => 500_000;

public Task<string> GetRowsAsync(int offset, int count) { /* ... */ }

// TS: ask for the window you're showing, keyed by the offset .NET answered with
const { Offset, Rows } = JSON.parse(await api.getRowsAsync(page * 200, 200));
setPages(prev => new Map(prev).set(Offset / 200, Rows));`}
                source="Samples/…/VirtualTable.tsx"
            >
                <VirtualTable />
            </Example>

            <Example
                title="Table"
                description="Presentational, and yours to drive. DataGrid sits on top of this when you want sorting and selection built in."
                code={`<Table>
    <TableHeader>
        <TableRow><TableHeaderCell>Name</TableHeaderCell></TableRow>
    </TableHeader>
    <TableBody>
        <TableRow><TableCell>
            <TableCellLayout media={<DocumentRegular />}>AOTrino.csproj</TableCellLayout>
        </TableCell></TableRow>
    </TableBody>
</Table>`}
            >
                <Table className={styles.grow} size="small">
                    <TableHeader>
                        <TableRow>
                            <TableHeaderCell>Name</TableHeaderCell>
                            <TableHeaderCell>Kind</TableHeaderCell>
                            <TableHeaderCell>Modified</TableHeaderCell>
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {files.map(f => (
                            <TableRow key={f.name}>
                                <TableCell>
                                    <TableCellLayout media={f.icon}>{f.name}</TableCellLayout>
                                </TableCell>
                                <TableCell>{f.kind}</TableCell>
                                <TableCell>{f.modified}</TableCell>
                            </TableRow>
                        ))}
                    </TableBody>
                </Table>
            </Example>

            <Example
                title="Tree"
                code={`<Tree aria-label="Files">
    <TreeItem itemType="branch">
        <TreeItemLayout>npm</TreeItemLayout>
        <Tree>
            <TreeItem itemType="leaf"><TreeItemLayout>client</TreeItemLayout></TreeItem>
        </Tree>
    </TreeItem>
</Tree>`}
            >
                <Tree aria-label="Packages" defaultOpenItems={["npm"]}>
                    <TreeItem itemType="branch" value="npm">
                        <TreeItemLayout iconBefore={<FolderRegular />}>npm</TreeItemLayout>
                        <Tree>
                            <TreeItem itemType="leaf">
                                <TreeItemLayout iconBefore={<DocumentRegular />}>@aotrino/client</TreeItemLayout>
                            </TreeItem>
                            <TreeItem itemType="leaf">
                                <TreeItemLayout iconBefore={<DocumentRegular />}>@aotrino/react</TreeItemLayout>
                            </TreeItem>
                            <TreeItem itemType="leaf">
                                <TreeItemLayout iconBefore={<DocumentRegular />}>@aotrino/fluent</TreeItemLayout>
                            </TreeItem>
                        </Tree>
                    </TreeItem>
                </Tree>
            </Example>

            <Example
                title="Accordion"
                code={`<Accordion collapsible>
    <AccordionItem value="1">
        <AccordionHeader>Header</AccordionHeader>
        <AccordionPanel>Panel</AccordionPanel>
    </AccordionItem>
</Accordion>`}
            >
                <Accordion className={styles.grow} collapsible defaultOpenItems="1">
                    <AccordionItem value="1">
                        <AccordionHeader>Why a WebView and not XAML?</AccordionHeader>
                        <AccordionPanel>
                            <Body1>
                                Because the shell is the only part that has to be native, and everything above it
                                is a solved problem with a much larger ecosystem.
                            </Body1>
                        </AccordionPanel>
                    </AccordionItem>
                    <AccordionItem value="2">
                        <AccordionHeader>Why not Electron?</AccordionHeader>
                        <AccordionPanel>
                            <Body1>
                                Because Windows already has a browser, and shipping a second one costs 150 MB and
                                a patch treadmill.
                            </Body1>
                        </AccordionPanel>
                    </AccordionItem>
                </Accordion>
            </Example>
        </>
    );
}

function Feedback() {
    const styles = useStyles();
    const toasterId = useId("toaster");
    const { dispatchToast } = useToastController(toasterId);

    return (
        <>
            <Example
                title="Spinner and ProgressBar"
                code={`<Spinner size="tiny" label="Working" />
<ProgressBar value={0.6} />
<ProgressBar />           {/* indeterminate */}`}
            >
                <div className={styles.stack}>
                    <div className={styles.row}>
                        <Spinner size="tiny" />
                        <Spinner size="small" label="Working" />
                    </div>
                    <ProgressBar value={0.6} />
                    <ProgressBar />
                    <ProgressBar value={1} color="success" />
                </div>
            </Example>

            <Example
                title="MessageBar"
                code={`<MessageBar intent="warning">
    <MessageBarBody>
        <MessageBarTitle>Careful</MessageBarTitle> ...
    </MessageBarBody>
</MessageBar>`}
            >
                <div className={styles.stack}>
                    <MessageBar intent="info">
                        <MessageBarBody>
                            <MessageBarTitle>Info</MessageBarTitle> Something worth knowing.
                        </MessageBarBody>
                    </MessageBar>
                    <MessageBar intent="success">
                        <MessageBarBody>
                            <MessageBarTitle>Success</MessageBarTitle> It worked.
                        </MessageBarBody>
                    </MessageBar>
                    <MessageBar intent="warning">
                        <MessageBarBody>
                            <MessageBarTitle>Warning</MessageBarTitle> Don't pair a disabled web security flag
                            with content you don't control.
                        </MessageBarBody>
                        <MessageBarActions>
                            <Button size="small">Read why</Button>
                        </MessageBarActions>
                    </MessageBar>
                    <MessageBar intent="error">
                        <MessageBarBody>
                            <MessageBarTitle>Error</MessageBarTitle> That .NET method always throws.
                        </MessageBarBody>
                    </MessageBar>
                </div>
            </Example>

            <Example
                title="Toast"
                description="A Toaster somewhere in the tree, then dispatch to it by id."
                code={`const toasterId = useId("toaster");
const { dispatchToast } = useToastController(toasterId);

dispatchToast(<Toast><ToastTitle>Saved</ToastTitle></Toast>, { intent: "success" });

<Toaster toasterId={toasterId} />`}
            >
                <Toaster toasterId={toasterId} />
                <Button
                    onClick={() =>
                        dispatchToast(
                            <Toast>
                                <ToastTitle>Saved</ToastTitle>
                                <ToastBody>Nothing was actually saved.</ToastBody>
                            </Toast>,
                            { intent: "success" },
                        )
                    }
                >
                    Show a toast
                </Button>
            </Example>

            <Example
                title="Skeleton"
                description="For the shape of content that hasn't arrived — which, on a bridge, is every first render."
                code={`<Skeleton>
    <SkeletonItem shape="circle" size={32} />
    <SkeletonItem />
</Skeleton>`}
            >
                <Skeleton className={styles.grow}>
                    <div className={styles.stack}>
                        <SkeletonItem shape="rectangle" size={16} />
                        <SkeletonItem shape="rectangle" size={16} />
                        <SkeletonItem shape="rectangle" size={16} />
                    </div>
                </Skeleton>
            </Example>
        </>
    );
}

function Surfaces() {
    const styles = useStyles();
    return (
        <>
            <Example
                title="Dialog"
                description="Modal, focus-trapped and escape-closing, without you writing any of that."
                code={`<Dialog>
    <DialogTrigger disableButtonEnhancement>
        <Button>Open</Button>
    </DialogTrigger>
    <DialogSurface>
        <DialogBody>
            <DialogTitle>Title</DialogTitle>
            <DialogContent>...</DialogContent>
            <DialogActions>
                <DialogTrigger disableButtonEnhancement>
                    <Button appearance="secondary">Close</Button>
                </DialogTrigger>
            </DialogActions>
        </DialogBody>
    </DialogSurface>
</Dialog>`}
            >
                <Dialog>
                    <DialogTrigger disableButtonEnhancement>
                        <Button appearance="primary">Open a dialog</Button>
                    </DialogTrigger>
                    <DialogSurface>
                        <DialogBody>
                            <DialogTitle>A real modal</DialogTitle>
                            <DialogContent>
                                <Body1>
                                    Focus is trapped here, Escape closes it, and the backdrop is inert — inside a
                                    window whose caption is also HTML.
                                </Body1>
                            </DialogContent>
                            <DialogActions>
                                <DialogTrigger disableButtonEnhancement>
                                    <Button appearance="secondary">Close</Button>
                                </DialogTrigger>
                                <Button appearance="primary">Do the thing</Button>
                            </DialogActions>
                        </DialogBody>
                    </DialogSurface>
                </Dialog>
            </Example>

            <Example
                title="Popover, Tooltip, Menu"
                description="All three are portalled — which is exactly what made the theme picker paint over the whole window once. See the note in AOTrinoProvider."
                code={`<Popover>
    <PopoverTrigger disableButtonEnhancement><Button>Popover</Button></PopoverTrigger>
    <PopoverSurface>...</PopoverSurface>
</Popover>

<Menu>
    <MenuTrigger disableButtonEnhancement><Button>Menu</Button></MenuTrigger>
    <MenuPopover><MenuList><MenuItem>Item</MenuItem></MenuList></MenuPopover>
</Menu>`}
            >
                <Popover>
                    <PopoverTrigger disableButtonEnhancement>
                        <Button>Popover</Button>
                    </PopoverTrigger>
                    <PopoverSurface>
                        <Body1>Anchored, flipped and shifted to stay on screen.</Body1>
                    </PopoverSurface>
                </Popover>

                <Tooltip content="A tooltip, positioned properly" relationship="label">
                    <Button icon={<Calendar24Regular />} />
                </Tooltip>

                <Menu>
                    <MenuTrigger disableButtonEnhancement>
                        <Button>Menu</Button>
                    </MenuTrigger>
                    <MenuPopover>
                        <MenuList>
                            <MenuItem icon={<EditRegular />}>Edit</MenuItem>
                            <MenuItem icon={<DocumentRegular />}>Duplicate</MenuItem>
                            <MenuItem disabled>Unavailable</MenuItem>
                        </MenuList>
                    </MenuPopover>
                </Menu>
            </Example>

            <Example
                title="Toolbar"
                code={`<Toolbar>
    <ToolbarButton icon={<TextBold24Regular />} />
    <ToolbarDivider />
</Toolbar>`}
            >
                <Toolbar aria-label="Formatting">
                    <ToolbarButton icon={<TextBold24Regular />} />
                    <ToolbarButton icon={<TextItalic24Regular />} />
                    <ToolbarButton icon={<TextUnderline24Regular />} />
                    <ToolbarDivider />
                    <ToolbarButton icon={<EditRegular />}>Edit</ToolbarButton>
                </Toolbar>
            </Example>

            <Example
                title="Tabs"
                description="The gallery's own nav is this, vertical. The page you're reading uses it twice."
                code={`<TabList defaultSelectedValue="a">
    <Tab value="a">First</Tab>
    <Tab value="b">Second</Tab>
</TabList>`}
            >
                <div className={styles.stack}>
                    <TabList defaultSelectedValue="a">
                        <Tab value="a">First</Tab>
                        <Tab value="b">Second</Tab>
                        <Tab value="c" disabled>Disabled</Tab>
                    </TabList>
                </div>
            </Example>
        </>
    );
}
